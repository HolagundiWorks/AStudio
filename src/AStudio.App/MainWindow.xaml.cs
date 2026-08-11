using System.Diagnostics;
using Aorms.Bridge;
using AStudio.App.Models;
using AStudio.App.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;
using WinRT.Interop;
using IoPath = System.IO.Path;
using Line = Microsoft.UI.Xaml.Shapes.Line;

namespace AStudio.App;

enum FocusDomain
{
    Overview,
    Brief,
    Drawings,
    Documents,
    Fees,
    Site,
}

public sealed partial class MainWindow : Window
{
    readonly AormsBridge _bridge;
    readonly LocalProjectsStore _projects;
    readonly LocalFeesStore _fees;
    readonly LocalDrawingsStore _drawings;
    readonly LocalDeliveryStore _delivery;
    readonly LocalClientsStore _clients;
    readonly LocalLedgerStore _decisions;
    readonly LocalLedgerStore _criticalNotes;
    readonly LocalLedgerStore _documents;
    readonly LocalLedgerStore _risks;
    readonly EstiOllamaClient _esti;
    readonly WellnessSession _wellness = new();
    readonly WellnessPrefs _wellnessPrefs = WellnessPrefs.Load();
    readonly WellnessReminderClock _wellnessReminders;
    readonly DispatcherTimer _clockTimer;
    readonly DispatcherTimer _wellnessTimer;
    readonly DispatcherTimer _wellnessReminderTimer;
    bool _wellnessFlyoutOpen;
    bool _wellnessPrefsUiBusy;
    WellnessReminder? _activeWellnessReminder;
    int _clockTicks;
    int _pomodoroDurationSec = 25 * 60;
    int _pomodoroLeftSec = 25 * 60;
    bool _pomodoroRunning;
    bool _pomodoroDragging;
    bool _pomodoroPressActive;
    Point _pomodoroPressOrigin;
    DispatcherTimer? _pomodoroClickTimer;
    StageId _stage = StageId.Home;
    FocusDomain _focusDomain = FocusDomain.Overview;
    bool _estiBusy;
    bool _rightSlotOpen;
    string? _focusProjectId;
    string? _selectedProjectId;
    string? _selectedFeeId;
    string? _selectedDrawingId;
    string? _selectedDeliveryId;
    string? _selectedClientId;
    string? _selectedDecisionId;
    string? _selectedNoteId;
    string? _selectedDocumentId;
    string? _selectedRiskId;
    string? _selectedTaskId;
    string? _siteFacet; // null = all; VISIT | SNAG | PROGRESS
    string _projectFilter = "";

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = false;
            ApplyWindowIcon();
            _bridge = AormsBridgeHost.CreateFromEnvironment();
            var dbPath = LocalProjectsStore.DefaultFirmDbPath();
            _projects = new LocalProjectsStore(dbPath);
            _fees = new LocalFeesStore(dbPath);
            _drawings = new LocalDrawingsStore(dbPath);
            _delivery = new LocalDeliveryStore(dbPath);
            _clients = new LocalClientsStore(dbPath);
            _decisions = new LocalLedgerStore(_projects.Connection, "local_decisions");
            _criticalNotes = new LocalLedgerStore(_projects.Connection, "local_critical_notes");
            _documents = new LocalLedgerStore(_projects.Connection, "local_documents");
            _risks = new LocalLedgerStore(_projects.Connection, "local_risks");
            _esti = new EstiOllamaClient();
            _wellnessPrefs.Clamp();
            _wellness.SetPattern(_wellnessPrefs.Pattern);
            _wellnessReminders = new WellnessReminderClock(() => _wellnessPrefs);
            _wellnessReminders.ArmFromNow();
            _wellnessReminders.Reminder += OnWellnessReminder;
            WireNavFlyouts();
            // Soft-neu is dual-offset slabs in XAML (NEU_RAISED) — not ThemeShadow.
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) =>
            {
                TickClock();
                TickPomodoro();
                _clockTicks++;
                if (_clockTicks % 5 == 0)
                    UpdateSyncStatus();
            };
            _clockTimer.Start();
            _wellnessTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _wellnessTimer.Tick += (_, _) => TickWellnessUi();
            _wellnessReminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _wellnessReminderTimer.Tick += (_, _) => _wellnessReminders.Tick();
            _wellnessReminderTimer.Start();
            _wellness.Completed += () => DispatcherQueue.TryEnqueue(() =>
            {
                RefreshWellnessUi();
                TrayText.Text = _wellness.Section switch
                {
                    WellnessSection.Stretch => "Stretch break complete",
                    WellnessSection.Eyes => "Eye break complete",
                    _ => "Breathing session complete",
                };
            });
            TickClock();
            UpdatePomodoroUi();
            ShowStage(StageId.Home);
            RefreshStatus("Ready.");
            _ = ProbeOllamaQuietAsync();
            ApplyConnectLicenceStatus();
            _ = SyncDemoDataIfNeededAsync();
        }
        catch (Exception ex)
        {
            LogStartupFailure(ex);
            throw;
        }
    }

    /// <summary>
    /// Licence SSO: import Connect session.json (already done in AormsBridgeHost).
    /// Never Activate from AStudio — Activate lives only in AORMS Connect.
    /// </summary>
    void ApplyConnectLicenceStatus()
    {
        _bridge.TryImportConnectSession(overwrite: true);
        if (_bridge.HubConfigured().HasSyncToken)
        {
            LicenceBtnLabel.Text = "Licensed";
            RefreshStatus($"Licence from Connect · {_bridge.HubConfigured().HubUrl}");
            return;
        }

        LicenceBtnLabel.Text = "Unbound";
        RefreshStatus(
            "Unbound — Activate licence in AORMS Connect, then Open AStudio (or Re-import Connect session).");
    }

    /// <summary>
    /// Import hub demo projects into firm.db + Flush projectStatus (local-dev only).
    /// Requires sync-demo-from-hub.cmd export (or Connect catalog).
    /// </summary>
    async Task SyncDemoDataIfNeededAsync()
    {
        try
        {
            var export = HubDemoImport.LoadExport();
            if (export.Count == 0 && ConnectCatalog.List().Count == 0)
                return;

            if (_projects.List().Count == 0)
            {
                ImportResultNote(HubDemoImport.ImportIntoFirm(_projects, export.Count > 0 ? export : null));
                ReloadProjects();
                RefreshHomeCapacity();
            }

            if (!_bridge.HubConfigured().SyncReady)
                return;

            // Publish any LOCAL/QUEUED projects so Ops DB sees demo meta.
            var needPublish = _projects.List().Any(p =>
                p.PublishState is "LOCAL" or "QUEUED" or "");
            if (!needPublish && _bridge.OutboxCounts().TotalPending == 0)
                return;

            SyncStatusText.Text = "Syncing demo…";
            var (queued, flush) = await HubDemoImport.PublishAllAsync(_bridge, _projects);
            if (flush.SkippedReason is not null)
            {
                TrayText.Text = $"Demo queued {queued}; flush skipped={flush.SkippedReason}";
                RefreshStatus($"Flush skipped={flush.SkippedReason}");
            }
            else
            {
                TrayText.Text = $"Demo synced · metaSent={flush.MetaSent}";
                RefreshStatus($"Demo Flush OK metaSent={flush.MetaSent} queued={queued}");
            }
            ReloadProjects();
            RefreshHomeCapacity();
            UpdateSyncStatus();
        }
        catch (Exception ex)
        {
            TrayText.Text = $"Demo sync failed: {ex.Message}";
            LogText.Text = ex.Message;
        }
    }

    void ImportResultNote(HubDemoImport.ImportResult result)
    {
        CatalogImportNote.Text = result.Note;
        TrayText.Text = result.Note;
        LogText.Text = result.Note;
    }

    async void SyncHubDemo_Click(object sender, RoutedEventArgs e)
    {
        // Force re-import of any missing + publish all.
        var result = HubDemoImport.ImportIntoFirm(_projects);
        ImportResultNote(result);
        ReloadProjects();
        RefreshHomeCapacity();
        if (!_bridge.HubConfigured().SyncReady)
        {
            TrayText.Text = "Imported locally — Activate in AORMS Connect, then Sync hub demo again.";
            ShowLicenceFlyout_Click(sender, e);
            return;
        }
        SyncStatusText.Text = "Syncing demo…";
        var (queued, flush) = await HubDemoImport.PublishAllAsync(_bridge, _projects);
        TrayText.Text = flush.SkippedReason is not null
            ? $"Queued {queued}; flush skipped={flush.SkippedReason}"
            : $"Demo synced · {flush.MetaSent} meta";
        RefreshStatus(TrayText.Text);
        ReloadProjects();
        UpdateSyncStatus();
    }

    void RefreshHomeCapacity()
    {
        try
        {
            LoadHome();
        }
        catch { /* startup race */ }
    }

    static void LogStartupFailure(Exception ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AStudio");
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, "startup-error.log"),
                $"{DateTime.Now:O}\n{ex}");
        }
        catch { /* best-effort */ }
    }

    void WireNavFlyouts()
    {
        foreach (var item in StudioNav.PeopleItems)
            PeopleFlyout.Items.Add(MakeNavItem(item.Id, item.Label, item.Blurb, PeopleMenu_Click));
        foreach (var item in StudioNav.OfficeItems)
            OfficeFlyout.Items.Add(MakeNavItem(item.Id, item.Label, item.Blurb, OfficeMenu_Click));
        foreach (var item in StudioNav.FinanceItems)
            FinanceFlyout.Items.Add(MakeNavItem(item.Id, item.Label, item.Blurb, FinanceMenu_Click));
        foreach (var item in StudioNav.AdminItems)
            AdminFlyout.Items.Add(MakeNavItem(item.Id, item.Label, item.Blurb, AdminMenu_Click));
    }

    static MenuFlyoutItem MakeNavItem(string id, string label, string blurb, RoutedEventHandler handler)
    {
        var mi = new MenuFlyoutItem { Text = label, Tag = $"{id}\n{label}\n{blurb}" };
        mi.Click += handler;
        return mi;
    }

    static bool TryNavTag(object sender, out string id, out string label, out string blurb)
    {
        id = label = blurb = "";
        if (sender is not MenuFlyoutItem { Tag: string tag }) return false;
        var parts = tag.Split('\n');
        if (parts.Length < 3) return false;
        id = parts[0];
        label = parts[1];
        blurb = parts[2];
        return true;
    }

    void PeopleMenu_Click(object sender, RoutedEventArgs e)
    {
        if (!TryNavTag(sender, out var id, out var label, out var blurb)) return;
        if (id == "work") ShowStage(StageId.Tasks);
        else ShowStage(StageId.Stub, label, blurb);
    }

    void OfficeMenu_Click(object sender, RoutedEventArgs e)
    {
        if (!TryNavTag(sender, out var id, out var label, out var blurb)) return;
        if (id == "proposals")
        {
            ShowStage(StageId.ProjectFocus);
            ShowFocusDomain(FocusDomain.Fees);
            return;
        }
        ShowStage(StageId.Stub, label, blurb);
    }

    void FinanceMenu_Click(object sender, RoutedEventArgs e)
    {
        if (!TryNavTag(sender, out var id, out var label, out var blurb)) return;
        if (id == "invoices")
        {
            ShowStage(StageId.ProjectFocus);
            ShowFocusDomain(FocusDomain.Fees);
            return;
        }
        ShowStage(StageId.Stub, label, blurb);
    }

    void AdminMenu_Click(object sender, RoutedEventArgs e)
    {
        if (!TryNavTag(sender, out var id, out var label, out var blurb)) return;
        if (id == "connection")
        {
            ShowStage(StageId.Home);
            TrayText.Text = "Use Sync on the taskbar; Activate in AORMS Connect.";
            return;
        }
        ShowStage(StageId.Stub, label, blurb);
    }

    void TickClock()
    {
        var now = DateTime.Now;
        var cx = 50.0;
        var cy = 50.0;
        SetHand(ClockHour, cx, cy, (now.Hour % 12 + now.Minute / 60.0) * 30.0, 22);
        SetHand(ClockMinute, cx, cy, now.Minute * 6.0, 32);
        // Hide second hand while Pomodoro runs (web peer).
        ClockSecond.Opacity = _pomodoroRunning ? 0 : 1;
        if (!_pomodoroRunning)
            SetHand(ClockSecond, cx, cy, now.Second * 6.0, 36);
    }

    static void SetHand(Line line, double cx, double cy, double degrees, double length)
    {
        var rad = (degrees - 90) * Math.PI / 180.0;
        line.X1 = cx;
        line.Y1 = cy;
        line.X2 = cx + Math.Cos(rad) * length;
        line.Y2 = cy + Math.Sin(rad) * length;
    }

    void TickPomodoro()
    {
        if (!_pomodoroRunning || _pomodoroDragging) return;
        if (_pomodoroLeftSec <= 0)
        {
            _pomodoroRunning = false;
            _pomodoroLeftSec = 0;
            UpdatePomodoroUi();
            TrayText.Text = "Pomodoro done";
            return;
        }
        _pomodoroLeftSec--;
        UpdatePomodoroUi();
    }

    void UpdatePomodoroUi()
    {
        var active = _pomodoroRunning || _pomodoroLeftSec < _pomodoroDurationSec || _pomodoroDragging;
        var label = FormatPomodoro(_pomodoroLeftSec);
        PomodoroLabel.Text = _pomodoroDragging
            ? $"{label} · set"
            : !_pomodoroRunning && !active
                ? $"{label} · Pomodoro"
                : _pomodoroRunning
                    ? $"{label} · running"
                    : $"{label} · paused";
        PomodoroLabel.Foreground = active
            ? BrushRes("HcwAccentBrush", Color.FromArgb(255, 0xFF, 0x4F, 0x18))
            : BrushRes("HcwMutedBrush", Color.FromArgb(255, 0x5C, 0x63, 0x70));

        var frac = Math.Clamp(_pomodoroLeftSec / 3600.0, 0, 1);
        UpdatePomodoroRing(frac);
    }

    void UpdatePomodoroRing(double frac)
    {
        const double outer = 127;
        const double cx = outer / 2;
        const double cy = outer / 2;
        const double r = outer * (72.5 / 165);

        var (hx, hy) = PointOnRing(cx, cy, r, frac);
        PomodoroArm.X1 = cx;
        PomodoroArm.Y1 = cy;
        PomodoroArm.X2 = hx;
        PomodoroArm.Y2 = hy;
        Canvas.SetLeft(PomodoroCrown, hx - 5);
        Canvas.SetTop(PomodoroCrown, hy - 5);
        Canvas.SetLeft(PomodoroCrownHit, hx - 14);
        Canvas.SetTop(PomodoroCrownHit, hy - 14);

        if (frac <= 0.001)
        {
            PomodoroArc.Data = null;
            return;
        }

        var (sx, sy) = PointOnRing(cx, cy, r, 0);
        var (ex, ey) = PointOnRing(cx, cy, r, Math.Min(frac, 0.9999));
        var fig = new PathFigure
        {
            StartPoint = new Point(sx, sy),
            IsClosed = false,
            IsFilled = false,
        };
        fig.Segments.Add(new ArcSegment
        {
            Point = new Point(ex, ey),
            Size = new Size(r, r),
            IsLargeArc = frac > 0.5,
            SweepDirection = SweepDirection.Clockwise,
            RotationAngle = 0,
        });
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        PomodoroArc.Data = geo;
    }

    static (double x, double y) PointOnRing(double cx, double cy, double r, double frac)
    {
        var a = (-90 + frac * 360) * Math.PI / 180.0;
        return (cx + r * Math.Cos(a), cy + r * Math.Sin(a));
    }

    static string FormatPomodoro(int seconds)
    {
        var s = Math.Max(0, seconds);
        return $"{s / 60:00}:{s % 60:00}";
    }

    void PomodoroToggle_Click(object sender, RoutedEventArgs e) => TogglePomodoro();

    void Pomodoro_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (_pomodoroDragging || _pomodoroPressActive) return;
        // Defer so double-tap can cancel and reset (web peer).
        _pomodoroClickTimer?.Stop();
        _pomodoroClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _pomodoroClickTimer.Tick += (_, _) =>
        {
            _pomodoroClickTimer?.Stop();
            _pomodoroClickTimer = null;
            TogglePomodoro();
        };
        _pomodoroClickTimer.Start();
        e.Handled = true;
    }

    void Pomodoro_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        _pomodoroClickTimer?.Stop();
        _pomodoroClickTimer = null;
        ResetPomodoro();
        e.Handled = true;
    }

    void TogglePomodoro()
    {
        if (_pomodoroLeftSec <= 0)
        {
            _pomodoroLeftSec = _pomodoroDurationSec;
            _pomodoroRunning = true;
        }
        else
        {
            _pomodoroRunning = !_pomodoroRunning;
        }
        TrayText.Text = _pomodoroRunning ? "Pomodoro running" : "Pomodoro paused";
        UpdatePomodoroUi();
    }

    void ResetPomodoro()
    {
        _pomodoroRunning = false;
        _pomodoroDragging = false;
        _pomodoroPressActive = false;
        _pomodoroLeftSec = _pomodoroDurationSec;
        UpdatePomodoroUi();
        TrayText.Text = "Pomodoro reset";
    }

    void PomodoroCrown_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_pomodoroRunning) return;
        _pomodoroPressActive = true;
        _pomodoroDragging = false;
        _pomodoroPressOrigin = e.GetCurrentPoint(PomodoroCanvas).Position;
        PomodoroCrownHit.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    void PomodoroCrown_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_pomodoroPressActive || _pomodoroRunning) return;
        var pt = e.GetCurrentPoint(PomodoroCanvas).Position;
        var dx = pt.X - _pomodoroPressOrigin.X;
        var dy = pt.Y - _pomodoroPressOrigin.Y;
        if (!_pomodoroDragging && (dx * dx + dy * dy) < 36) return; // 6px threshold
        _pomodoroDragging = true;
        ApplyPomodoroDrag(pt);
        e.Handled = true;
    }

    void PomodoroCrown_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pomodoroPressActive) return;
        var wasDragging = _pomodoroDragging;
        _pomodoroPressActive = false;
        _pomodoroDragging = false;
        try { PomodoroCrownHit.ReleasePointerCapture(e.Pointer); } catch { /* ignore */ }

        if (wasDragging)
        {
            // Snap to 5-minute steps (5–60), web peer.
            var minutes = Math.Max(5, Math.Min(60, (int)Math.Round(_pomodoroLeftSec / 60.0 / 5.0) * 5));
            _pomodoroDurationSec = minutes * 60;
            _pomodoroLeftSec = _pomodoroDurationSec;
            UpdatePomodoroUi();
            TrayText.Text = $"Pomodoro set · {minutes} min";
        }
        else
        {
            // Crown click without drag = toggle (same as dial).
            TogglePomodoro();
        }
        e.Handled = true;
    }

    void ApplyPomodoroDrag(Point pt)
    {
        const double cx = 63.5;
        const double cy = 63.5;
        var deg = Math.Atan2(pt.Y - cy, pt.X - cx) * 180.0 / Math.PI;
        var fromTop = (deg + 90) % 360;
        if (fromTop < 0) fromTop += 360;
        var frac = Math.Max(5.0 / 60.0, Math.Min(1.0, fromTop / 360.0));
        _pomodoroLeftSec = (int)Math.Round(frac * 3600);
        UpdatePomodoroUi();
    }

    void CalcToggle_Click(object sender, RoutedEventArgs e)
    {
        RecalcResult();
        CalcFlyout.ShowAt(NavCalcBtn);
        CalcExprBox.Focus(FocusState.Programmatic);
    }

    void CalcExpr_Changed(object sender, TextChangedEventArgs e) => RecalcResult();

    void CalcImperial_Toggled(object sender, RoutedEventArgs e) => RecalcResult();

    void CalcExpr_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        var v = OfficeCalculator.Eval(CalcExprBox.Text);
        if (v is null) return;
        CalcExprBox.Text = v.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        RecalcResult();
        e.Handled = true;
    }

    void RecalcResult()
    {
        var expr = CalcExprBox.Text ?? "";
        var imperial = CalcImperialToggle.IsOn;
        if (string.IsNullOrWhiteSpace(expr))
        {
            CalcResultText.Text = imperial ? "0'0\"" : "0 m";
            return;
        }
        var v = OfficeCalculator.Eval(expr);
        CalcResultText.Text = v is null ? "—" : $"= {OfficeCalculator.Format(v.Value, imperial)}";
    }

    void ShowStage(StageId stage, string? stubTitle = null, string? stubBlurb = null)
    {
        _stage = stage;
        PanelHome.Visibility = stage == StageId.Home ? Visibility.Visible : Visibility.Collapsed;
        PanelProjects.Visibility = stage == StageId.Projects ? Visibility.Visible : Visibility.Collapsed;
        PanelFocus.Visibility = stage == StageId.ProjectFocus ? Visibility.Visible : Visibility.Collapsed;
        PanelClients.Visibility = stage == StageId.Clients ? Visibility.Visible : Visibility.Collapsed;
        PanelTasks.Visibility = stage == StageId.Tasks ? Visibility.Visible : Visibility.Collapsed;
        PanelStub.Visibility = stage == StageId.Stub ? Visibility.Visible : Visibility.Collapsed;

        StyleNav(NavHomeBtn, stage == StageId.Home);
        StyleNav(NavProjectsBtn, stage is StageId.Projects or StageId.ProjectFocus);
        StyleNav(NavClientsBtn, stage == StageId.Clients);
        // Menus stay inactive unless their stub/work is showing — leave unstyled for flyouts
        StyleNav(NavPeopleBtn, stage == StageId.Tasks);
        StyleNav(NavOfficeBtn, false);
        StyleNav(NavFinanceBtn, stage == StageId.ProjectFocus && _focusDomain == FocusDomain.Fees);
        StyleNav(NavAdminBtn, false);

        DockImportBtn.Visibility = stage == StageId.Projects ? Visibility.Visible : Visibility.Collapsed;

        if (stage == StageId.Stub)
        {
            StubTitle.Text = stubTitle ?? "Coming on desktop";
            StubBlurb.Text = stubBlurb ?? "This module is on the web IA; desktop depth lands later.";
        }

        ApplyDockLabels();
        TrayText.Text = stage == StageId.Stub
            ? $"AStudio · {stubTitle}"
            : $"AStudio · {stage}";

        switch (stage)
        {
            case StageId.Home:
                LoadHome();
                _ = ProbeOllamaQuietAsync();
                break;
            case StageId.Projects:
                ReloadProjects();
                break;
            case StageId.ProjectFocus:
                LoadFocusForm();
                break;
            case StageId.Clients:
                ReloadClients();
                break;
            case StageId.Tasks:
                if (!string.IsNullOrEmpty(_focusProjectId) &&
                    string.IsNullOrWhiteSpace(TaskProjectBox.Text))
                    TaskProjectBox.Text = _focusProjectId;
                ReloadTasks();
                break;
        }

        UpdateDockEnabled();
    }

    void ApplyDockLabels()
    {
        // Kit zones: CENTER = create/save · RIGHT = commit — update labels only (keep icons).
        if (_stage == StageId.ProjectFocus)
        {
            DockCreateLabel.Text = _focusDomain switch
            {
                FocusDomain.Overview => "Save ledger",
                FocusDomain.Fees => "Save fee",
                FocusDomain.Drawings => "Save drawing",
                FocusDomain.Documents => "Save document",
                FocusDomain.Site => "Save site item",
                _ => "Save brief",
            };
            DockCommitLabel.Text = _focusDomain switch
            {
                FocusDomain.Overview => "Publish approval",
                FocusDomain.Fees => "Publish invoice",
                FocusDomain.Drawings => "Publish register",
                FocusDomain.Documents => "Publish document",
                FocusDomain.Site => "Publish progress",
                _ => "Publish status",
            };
            DockCreateIcon.Glyph = "\uE710"; // Add
            return;
        }

        DockCreateLabel.Text = _stage switch
        {
            StageId.Projects => "Save project",
            StageId.Clients => "Save client",
            StageId.Home => "Probe Ollama",
            StageId.Tasks => "Save local",
            _ => "Save",
        };
        DockCommitLabel.Text = _stage switch
        {
            StageId.Projects or StageId.ProjectFocus => "Publish status",
            StageId.Clients => "Publish client",
            StageId.Home => "Flush meta",
            StageId.Tasks => "Publish to hub",
            _ => "Publish",
        };
        DockCreateIcon.Glyph = _stage == StageId.Home ? "\uE895" : "\uE710";
    }

    void UpdateDockEnabled()
    {
        var hasFocusProject = ResolveFocusProjectId() is not null;
        switch (_stage)
        {
            case StageId.ProjectFocus:
                DockCreateBtn.IsEnabled = hasFocusProject;
                DockCommitBtn.IsEnabled = hasFocusProject;
                break;
            case StageId.Projects:
                DockCreateBtn.IsEnabled = true;
                DockCommitBtn.IsEnabled = hasFocusProject || _selectedProjectId is not null
                    || _projects.List().Count > 0;
                break;
            case StageId.Stub:
                DockCreateBtn.IsEnabled = false;
                DockCommitBtn.IsEnabled = false;
                break;
            default:
                DockCreateBtn.IsEnabled = true;
                DockCommitBtn.IsEnabled = true;
                break;
        }
    }

    string? ResolveFocusProjectId() => _focusProjectId ?? _selectedProjectId;

    void ApplyWindowIcon()
    {
        try
        {
            var path = IoPath.Combine(AppContext.BaseDirectory, "Assets", "favicon.ico");
            if (!File.Exists(path))
                path = IoPath.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (!File.Exists(path)) return;
            var hwnd = WindowNative.GetWindowHandle(this);
            var id = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow.GetFromWindowId(id).SetIcon(path);
        }
        catch
        {
            /* best-effort branding */
        }
    }

    static Brush BrushRes(string key, Color fallback)
    {
        if (Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b)
            return b;
        return new SolidColorBrush(fallback);
    }

    /// <summary>Web navSx peer — transparent face + 2px accent underline (not orange fill).</summary>
    static void StyleNav(Button btn, bool active)
    {
        var accent = BrushRes("HcwAccentBrush", Color.FromArgb(255, 0xFF, 0x4F, 0x18));
        var muted = BrushRes("HcwMutedBrush", Color.FromArgb(255, 0x5C, 0x63, 0x70));
        var transparent = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        btn.Background = transparent;
        btn.BorderThickness = new Thickness(0, 0, 0, 2);
        btn.BorderBrush = active ? accent : transparent;
        var fg = active ? accent : muted;
        btn.Foreground = fg;
        ApplyForeground(btn.Content, fg);
    }

    static void ApplyForeground(object? content, Brush fg)
    {
        switch (content)
        {
            case FontIcon fi:
                fi.Foreground = fg;
                break;
            case TextBlock tb:
                tb.Foreground = fg;
                break;
            case Panel panel:
                foreach (var child in panel.Children)
                    ApplyForeground(child, fg);
                break;
        }
    }

    void WellnessToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_wellnessFlyoutOpen) CloseWellnessPanel();
        else OpenWellnessFlyout();
    }

    void OpenWellnessFlyout(WellnessSection? section = null)
    {
        if (section is { } s) _wellness.SetSection(s);
        LoadWellnessPrefsIntoUi();
        RefreshWellnessUi();
        WellnessPanelHost.Visibility = Visibility.Visible;
        WellnessDismissCatcher.Visibility = Visibility.Visible;
        _wellnessFlyoutOpen = true;
        if (!_wellnessTimer.IsEnabled) _wellnessTimer.Start();
        StyleNav(NavWellnessBtn, true);
    }

    void CloseWellnessPanel()
    {
        if (!_wellnessFlyoutOpen) return;
        PersistWellnessPrefsFromUi();
        WellnessPanelHost.Visibility = Visibility.Collapsed;
        WellnessDismissCatcher.Visibility = Visibility.Collapsed;
        _wellnessFlyoutOpen = false;
        if (!_wellness.Running) _wellnessTimer.Stop();
        StyleNav(NavWellnessBtn, false);
    }

    void WellnessDismissCatcher_Tapped(object sender, TappedRoutedEventArgs e) =>
        CloseWellnessPanel();

    void WellnessTabBreathe_Click(object sender, RoutedEventArgs e)
    {
        _wellness.SetSection(WellnessSection.Breathe);
        RefreshWellnessUi();
    }

    void WellnessTabStretch_Click(object sender, RoutedEventArgs e)
    {
        _wellness.SetSection(WellnessSection.Stretch);
        RefreshWellnessUi();
    }

    void WellnessTabEyes_Click(object sender, RoutedEventArgs e)
    {
        _wellness.SetSection(WellnessSection.Eyes);
        RefreshWellnessUi();
    }

    void WellnessPattern_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
        {
            _wellness.SetPattern(key);
            _wellnessPrefs.Pattern = key;
            _wellnessPrefs.Save();
        }
        RefreshWellnessUi();
    }

    void WellnessPlay_Click(object sender, RoutedEventArgs e)
    {
        _wellness.Toggle();
        if (_wellness.Running && !_wellnessTimer.IsEnabled)
            _wellnessTimer.Start();
        RefreshWellnessUi();
    }

    void WellnessPrefs_Changed(object sender, RoutedEventArgs e)
    {
        if (_wellnessPrefsUiBusy) return;
        PersistWellnessPrefsFromUi();
    }

    void WellnessPrefsNumber_Changed(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (_wellnessPrefsUiBusy) return;
        PersistWellnessPrefsFromUi();
    }

    void LoadWellnessPrefsIntoUi()
    {
        _wellnessPrefsUiBusy = true;
        try
        {
            WellnessHydrationToggle.IsChecked = _wellnessPrefs.HydrationEnabled;
            WellnessHydrationMin.Value = _wellnessPrefs.HydrationMin;
            WellnessStretchToggle.IsChecked = _wellnessPrefs.StretchEnabled;
            WellnessStretchMin.Value = _wellnessPrefs.StretchMin;
            WellnessEyesToggle.IsChecked = _wellnessPrefs.EyeExerciseEnabled;
            WellnessEyesMin.Value = _wellnessPrefs.EyeExerciseMin;
        }
        finally
        {
            _wellnessPrefsUiBusy = false;
        }
    }

    void PersistWellnessPrefsFromUi()
    {
        _wellnessPrefs.HydrationEnabled = WellnessHydrationToggle.IsChecked == true;
        _wellnessPrefs.HydrationMin = (int)Math.Clamp(WellnessHydrationMin.Value, 1, 240);
        _wellnessPrefs.StretchEnabled = WellnessStretchToggle.IsChecked == true;
        _wellnessPrefs.StretchMin = (int)Math.Clamp(WellnessStretchMin.Value, 1, 240);
        _wellnessPrefs.EyeExerciseEnabled = WellnessEyesToggle.IsChecked == true;
        _wellnessPrefs.EyeExerciseMin = (int)Math.Clamp(WellnessEyesMin.Value, 1, 240);
        _wellnessPrefs.Pattern = _wellness.Pattern.Key;
        _wellnessPrefs.Clamp();
        _wellnessPrefs.Save();
    }

    void OnWellnessReminder(WellnessReminder reminder)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (reminder.Kind == "hydrate")
            {
                TrayText.Text = reminder.Title;
                LogText.Text = reminder.Subtitle;
                return;
            }
            _activeWellnessReminder = reminder;
            WellnessReminderTitle.Text = reminder.Title;
            WellnessReminderSubtitle.Text = reminder.Subtitle;
            WellnessReminderIcon.Glyph = reminder.Kind == "eyes" ? "\uE609" : "\uE95B";
            WellnessReminderBanner.Visibility = Visibility.Visible;
        });
    }

    void WellnessReminderStart_Click(object sender, RoutedEventArgs e)
    {
        var kind = _activeWellnessReminder?.Kind;
        WellnessReminderBanner.Visibility = Visibility.Collapsed;
        _activeWellnessReminder = null;
        var section = kind == "eyes" ? WellnessSection.Eyes : WellnessSection.Stretch;
        OpenWellnessFlyout(section);
        if (!_wellness.Running) WellnessPlay_Click(sender, e);
    }

    void WellnessReminderDismiss_Click(object sender, RoutedEventArgs e)
    {
        WellnessReminderBanner.Visibility = Visibility.Collapsed;
        _activeWellnessReminder = null;
    }

    void TickWellnessUi()
    {
        if (!_wellness.Running && !_wellnessFlyoutOpen)
        {
            _wellnessTimer.Stop();
            return;
        }
        _wellness.Tick();
        RefreshWellnessUi();
    }

    void StyleWellnessChip(Button btn, bool active)
    {
        var key = active ? "HcwWellnessChipActive" : "HcwWellnessChip";
        if (Application.Current.Resources.TryGetValue(key, out var style) && style is Style s)
            btn.Style = s;
    }

    void RefreshWellnessUi()
    {
        StyleWellnessChip(WellnessTabBreathe, _wellness.Section == WellnessSection.Breathe);
        StyleWellnessChip(WellnessTabStretch, _wellness.Section == WellnessSection.Stretch);
        StyleWellnessChip(WellnessTabEyes, _wellness.Section == WellnessSection.Eyes);

        var breathe = _wellness.Section == WellnessSection.Breathe;
        WellnessPatternRow.Visibility = breathe ? Visibility.Visible : Visibility.Collapsed;
        StyleWellnessChip(WellnessPatRelax, _wellness.Pattern.Key == "relax");
        StyleWellnessChip(WellnessPatFocus, _wellness.Pattern.Key == "focus");
        StyleWellnessChip(WellnessPatCalm, _wellness.Pattern.Key == "anxiety");
        StyleWellnessChip(WellnessPatDaily, _wellness.Pattern.Key == "daily");

        WellnessPhaseText.Text = _wellness.PhaseLabel;
        WellnessTimerText.Text = _wellness.Running
            ? $"{WellnessSession.FormatMmSs(_wellness.SessionLeftSec)} left"
            : "Press play to begin";

        WellnessBreathHost.Visibility = breathe ? Visibility.Visible : Visibility.Collapsed;
        WellnessStretchHost.Visibility = _wellness.Section == WellnessSection.Stretch
            ? Visibility.Visible : Visibility.Collapsed;
        WellnessEyeHost.Visibility = _wellness.Section == WellnessSection.Eyes
            ? Visibility.Visible : Visibility.Collapsed;

        // Breath: neumorphic orb scale (web esti-breath-orb JS transform).
        WellnessOrbScale.ScaleX = _wellness.OrbScale;
        WellnessOrbScale.ScaleY = _wellness.OrbScale;
        WellnessPhaseCountText.Text = _wellness.Running && breathe && _wellness.PhaseLeftSec > 0
            ? _wellness.PhaseLeftSec.ToString()
            : "";
        WellnessPhaseCountText.Visibility =
            WellnessPhaseCountText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Stretch / eyes: keyframe peers from glass.scss
        WellnessStretchXform.Rotation = _wellness.GlyphRotate;
        WellnessStretchXform.TranslateY = _wellness.GlyphTranslateY;
        WellnessStretchXform.ScaleX = _wellness.GlyphScaleX;
        WellnessStretchXform.ScaleY = _wellness.GlyphScaleY;

        WellnessEyeXform.ScaleY = _wellness.GlyphScaleY;
        WellnessEyeHost.Opacity = _wellness.GlyphOpacity;
        WellnessIrisXform.TranslateX = _wellness.IrisX;
        WellnessIrisXform.TranslateY = _wellness.IrisY;
        WellnessIrisXform.ScaleX = _wellness.IrisScale;
        WellnessIrisXform.ScaleY = _wellness.IrisScale;

        WellnessStepText.Text = breathe ? "" : _wellness.StepProgressLabel;

        if (breathe)
        {
            WellnessCueText.Text = _wellness.Running
                ? _wellness.Pattern.DurationLabel
                : _wellness.Pattern.Goal;
            WellnessGoalText.Text = _wellness.Pattern.Name;
        }
        else
        {
            var step = _wellness.Step;
            WellnessCueText.Text = step is null
                ? "Press play to begin"
                : $"{step.Name} — {step.Cue}";
            WellnessGoalText.Text = step?.Name ?? "";
        }

        WellnessPlayIcon.Glyph = _wellness.Running ? "\uE71A" : "\uE768";
        ToolTipService.SetToolTip(WellnessPlayBtn, _wellness.Running ? "Stop" : "Start");
    }


    void NavHome_Click(object sender, RoutedEventArgs e) => ShowStage(StageId.Home);
    void NavProjects_Click(object sender, RoutedEventArgs e) => ShowStage(StageId.Projects);
    void NavClients_Click(object sender, RoutedEventArgs e) => ShowStage(StageId.Clients);
    void NavPeople_Click(object sender, RoutedEventArgs e) => NavPeopleBtn.Flyout?.ShowAt(NavPeopleBtn);
    void NavOffice_Click(object sender, RoutedEventArgs e) => NavOfficeBtn.Flyout?.ShowAt(NavOfficeBtn);
    void NavFinance_Click(object sender, RoutedEventArgs e) => NavFinanceBtn.Flyout?.ShowAt(NavFinanceBtn);
    void NavAdmin_Click(object sender, RoutedEventArgs e) => NavAdminBtn.Flyout?.ShowAt(NavAdminBtn);

    void ToggleRightSlot_Click(object sender, RoutedEventArgs e)
    {
        _rightSlotOpen = !_rightSlotOpen;
        RightSlotCol.Width = _rightSlotOpen ? new GridLength(360) : new GridLength(0);
        RightSlotPanel.Visibility = _rightSlotOpen ? Visibility.Visible : Visibility.Collapsed;
        StyleNav(AskEstiRibbonBtn, _rightSlotOpen);
        if (_rightSlotOpen) _ = ProbeOllamaQuietAsync();
    }

    void AccountStub_Click(object sender, RoutedEventArgs e) =>
        ShowStage(StageId.Stub, "Account", "Account / identity hub — Activate licence in AORMS Connect; this app imports session.json.");

    void ShowLicenceFlyout_Click(object sender, RoutedEventArgs e)
    {
        ApplyConnectLicenceStatus();
        LicenceFlyout.ShowAt(LicenceBtn);
    }

    void ReimportConnectSession_Click(object sender, RoutedEventArgs e)
    {
        var imported = _bridge.TryImportConnectSession(overwrite: true);
        ApplyConnectLicenceStatus();
        RefreshStatus(
            imported
                ? "Imported Connect session.json into AStudio firm.db."
                : "No Connect session.json — Activate in AORMS Connect first.");
    }

    void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        var q = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrEmpty(q)) return;
        if (q.Contains("project")) ShowStage(StageId.Projects);
        else if (q.Contains("client")) ShowStage(StageId.Clients);
        else if (q.Contains("task") || q.Contains("work")) ShowStage(StageId.Tasks);
        else if (q.Contains("esti") || q.Contains("ask"))
        {
            if (!_rightSlotOpen) ToggleRightSlot_Click(sender, e);
        }
        else if (q.Contains("home") || q.Contains("studio")) ShowStage(StageId.Home);
        else ShowStage(StageId.Stub, "Search", $"No desktop match for “{SearchBox.Text}”. Try Projects, Clients, Tasks, Ask ESTI.");
    }

    void LoadHome()
    {
        var projects = _projects.List();
        var clients = _clients.List();
        var tasks = _bridge.Db.ListLocalTasks();
        var openTasks = tasks.Count(t => !string.Equals(t.Status, "DONE", StringComparison.OrdinalIgnoreCase));
        var outbox = _bridge.OutboxCounts();
        var cfg = _bridge.HubConfigured();

        HomeKpiProjects.Text = projects.Count.ToString();
        HomeKpiClients.Text = clients.Count.ToString();
        HomeKpiTasks.Text = openTasks.ToString();
        HomeKpiSync.Text = outbox.TotalPending.ToString();
        HomeCapacityText.Text =
            $"projects={projects.Count}  clients={clients.Count}  tasks={openTasks}  " +
            $"focus={ResolveFocusProjectId() ?? "—"}";

        var attention = new List<AttentionRow>();
        if (!cfg.HasSyncToken)
            attention.Add(new AttentionRow { Text = "Hub unbound — Activate in AORMS Connect, then re-open or Re-import session." });
        else if (outbox.TotalPending > 0)
            attention.Add(new AttentionRow { Text = $"{outbox.TotalPending} pending sync item(s) — Flush from Sync chip." });
        var unpublished = projects.Count(p => p.PublishState is "LOCAL" or "QUEUED" or "");
        if (unpublished > 0)
            attention.Add(new AttentionRow { Text = $"{unpublished} project(s) not published (LOCAL/QUEUED)." });
        if (projects.Count == 0)
            attention.Add(new AttentionRow { Text = "No projects — Import Connect / Sync hub demo, or Save a project." });
        else if (ResolveFocusProjectId() is null)
            attention.Add(new AttentionRow { Text = "No project in Focus — open one from Projects." });
        if (attention.Count == 0)
            attention.Add(new AttentionRow { Text = "Office clear — no attention items." });
        HomeAttentionList.ItemsSource = attention;

        HomeBriefLine.Text = attention[0].Text;
        HomeHubText.Text =
            cfg.HasSyncToken
                ? $"Hub · {(cfg.SyncReady ? "ready" : "offline")} · {cfg.HubUrl}"
                : "Hub unbound — Activate in AORMS Connect.";
        HealthText.Text = projects.Count == 0 ? "Office · empty" : $"Office · {projects.Count} projects";
        RefreshStatus();
    }

    void RefreshStatus(string? note = null)
    {
        var cfg = _bridge.HubConfigured();
        var outbox = _bridge.OutboxCounts();
        HubStatusText.Text =
            $"hub={cfg.HubUrl}\n" +
            $"licenseApi={cfg.LicenseApiUrl}\n" +
            $"hasSyncToken={cfg.HasSyncToken}  syncReady={cfg.SyncReady}\n" +
            $"outbox meta={outbox.PendingMeta}  artifacts={outbox.PendingArtifacts}";
        UpdateSyncStatus(cfg, outbox);
        if (!string.IsNullOrWhiteSpace(note))
            LogText.Text = note;
    }

    /// <summary>Taskbar sync chip — peer to web SyncQueueChip (bound · pending · idle).</summary>
    void UpdateSyncStatus(HubConfigured? cfg = null, OutboxCounts? outbox = null)
    {
        cfg ??= _bridge.HubConfigured();
        outbox ??= _bridge.OutboxCounts();

        var muted = BrushRes("HcwMutedBrush", Color.FromArgb(255, 0x5C, 0x63, 0x70));
        var accent = BrushRes("HcwAccentBrush", Color.FromArgb(255, 0xFF, 0x4F, 0x18));
        var ink = BrushRes("HcwInkBrush", Color.FromArgb(255, 0x14, 0x15, 0x17));

        string label;
        string tooltip;
        string glyph;
        Brush tone;

        if (!cfg.HasSyncToken)
        {
            label = "Unbound";
            tooltip = "Hub sync not bound — Activate licence in AORMS Connect, then Re-import session.";
            glyph = "\uE8CE"; // CloudOff
            tone = muted;
        }
        else if (!cfg.SyncReady)
        {
            label = "Offline";
            tooltip = $"Token present but hub not ready · {cfg.HubUrl}";
            glyph = "\uE7BA"; // Warning
            tone = muted;
        }
        else if (outbox.TotalPending > 0)
        {
            label = $"Pending {outbox.TotalPending}";
            tooltip =
                $"{outbox.PendingMeta} meta · {outbox.PendingArtifacts} artifacts waiting — click Sync or this chip to flush.";
            glyph = "\uE895"; // Sync
            tone = accent;
        }
        else
        {
            label = "Synced";
            tooltip = $"Hub idle · {cfg.HubUrl}";
            glyph = "\uE73E"; // Accept / check
            tone = ink;
        }

        SyncStatusText.Text = label;
        SyncStatusText.Foreground = ink;
        SyncStatusDot.Fill = tone;
        SyncStatusIcon.Glyph = glyph;
        SyncStatusIcon.Foreground = tone;
        ToolTipService.SetToolTip(SyncStatusChip, tooltip);
        SyncFlushBtn.IsEnabled = cfg.SyncReady || outbox.TotalPending > 0;
    }

    void SyncStatusChip_Tapped(object sender, TappedRoutedEventArgs e)
    {
        var cfg = _bridge.HubConfigured();
        if (!cfg.HasSyncToken)
        {
            ShowLicenceFlyout_Click(sender, e);
            return;
        }
        Flush_Click(sender, e);
    }

    async Task ProbeOllamaQuietAsync()
    {
        var probe = await _esti.ProbeAsync();
        EstiStatusText.Text = $"{probe.Note} · {_esti.BaseUrl}";
        LocalAiBadge.Text = probe.Reachable
            ? $"Local AI · {_esti.Model}"
            : "Local AI · offline";
        LocalAiBadge.Opacity = probe.Reachable ? 0.85 : 0.45;
    }

    async void ProbeOllama_Click(object sender, RoutedEventArgs e)
    {
        if (_estiBusy) return;
        _estiBusy = true;
        try
        {
            EstiStatusText.Text = "Probing Ollama…";
            var probe = await _esti.ProbeAsync();
            EstiStatusText.Text = $"{probe.Note} · {_esti.BaseUrl}";
            LocalAiBadge.Text = probe.Reachable
                ? $"Local AI · {_esti.Model}"
                : "Local AI · offline";
            LocalAiBadge.Opacity = probe.Reachable ? 0.85 : 0.45;
            TrayText.Text = probe.Reachable ? "Ollama reachable" : "Ollama offline";
            LogText.Text = probe.Note;
        }
        finally
        {
            _estiBusy = false;
        }
    }

    async void AskEsti_Click(object sender, RoutedEventArgs e)
    {
        if (_estiBusy) return;
        var q = EstiPromptBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(q))
        {
            TrayText.Text = "Enter a question for ESTI.";
            return;
        }
        _estiBusy = true;
        try
        {
            EstiReplyText.Text = "Asking local Ollama…";
            TrayText.Text = "ESTI thinking…";
            var result = await _esti.AskAsync(q, BuildEstiContext());
            EstiReplyText.Text = result.Ok ? result.Reply : result.Note;
            TrayText.Text = result.Ok ? "ESTI reply ready (local only)" : "ESTI ask failed";
            LogText.Text = result.Note;
            // Never enqueue transcripts — SYNC-CONTRACT "Never sync".
        }
        finally
        {
            _estiBusy = false;
        }
    }

    string BuildEstiContext()
    {
        var id = ResolveFocusProjectId();
        if (id is null) return "No project in Focus.";
        var p = _projects.Get(id);
        if (p is null) return $"Focus project {id} missing from firm.db.";
        var feeN = _fees.ListByProject(id).Count;
        var dwgN = _drawings.ListByProject(id).Count;
        var delN = _delivery.ListByProject(id).Count;
        return
            $"id={p.ProjectId} ref={p.ProjectRef} title={p.Title} status={p.Status} phase={p.Phase}\n" +
            $"notes={TrimCtx(p.Notes, 200)}\n" +
            $"counts: fees={feeN} drawings={dwgN} delivery={delN}";
    }

    static string TrimCtx(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "…";

    void Refresh_Click(object sender, RoutedEventArgs e) => RefreshStatus("Status refreshed.");

    async void Flush_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LogText.Text = "Syncing…";
            SyncStatusText.Text = "Syncing…";
            var result = await _bridge.FlushAsync();
            if (result.SkippedReason is not null)
            {
                TrayText.Text = $"Sync skipped";
                RefreshStatus($"Flush skipped={result.SkippedReason}");
            }
            else
            {
                TrayText.Text = $"Synced · {result.MetaSent + result.ArtifactsSent}";
                RefreshStatus($"Flush OK metaSent={result.MetaSent} artSent={result.ArtifactsSent}");
            }
        }
        catch (Exception ex)
        {
            TrayText.Text = "Sync failed";
            RefreshStatus($"Flush failed: {ex.Message}");
        }
    }

    void ProjectSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _projectFilter = ProjectSearchBox.Text?.Trim() ?? "";
        ReloadProjects();
    }

    void ProjectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectsListView.SelectedItem is ProjectRow row)
            _selectedProjectId = row.ProjectId;
        UpdateDockEnabled();
    }

    void ProjectsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
        OpenSelectedFocus_Click(sender, e);

    void ReloadProjects()
    {
        var rows = _projects.List();
        IEnumerable<LocalProject> filtered = rows;
        if (!string.IsNullOrEmpty(_projectFilter))
        {
            var q = _projectFilter;
            filtered = rows.Where(r =>
                r.ProjectRef.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Status.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Phase.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        var items = filtered.Select(r => new ProjectRow
        {
            ProjectId = r.ProjectId,
            Ref = r.ProjectRef,
            Title = r.Title,
            Status = r.Status,
            Phase = r.Phase,
            PublishState = r.PublishState,
        }).ToList();
        ProjectsListView.ItemsSource = items;
        ProjectListText.Text = items.Count == 0
            ? "(empty — save a project, or Import from Connect)"
            : $"{items.Count} project(s)";
        if (_selectedProjectId is null && items.Count > 0)
            _selectedProjectId = items[0].ProjectId;
        if (_selectedProjectId is not null)
        {
            var match = items.FirstOrDefault(i => i.ProjectId == _selectedProjectId);
            if (match is not null) ProjectsListView.SelectedItem = match;
        }
        UpdateDockEnabled();
    }

    void LoadFocusForm()
    {
        var id = ResolveFocusProjectId();
        if (id is null)
        {
            FocusSubtitle.Text = "No project in focus.";
            FocusEmptyPanel.Visibility = Visibility.Visible;
            FocusWorkPanel.Visibility = Visibility.Collapsed;
            FocusEmptyCopy.Text =
                "Open Projects, pick a local project (or Import from Connect), then Open in Focus. " +
                "Overview · Brief · Drawings · Documents · Fees · Site.";
            FocusTitleBox.Text = "";
            FocusRefBox.Text = "";
            FocusStatusBox.Text = "";
            FocusPhaseBox.Text = "";
            FocusNotesBox.Text = "";
            FocusClientIdBox.Text = "";
            FocusJurisdictionBox.Text = "";
            FocusSiteAddressBox.Text = "";
            FocusWorkTypeBox.Text = "";
            FocusMetaText.Text = "";
            FocusEngineText.Text = "";
            ApplyDockLabels();
            UpdateDockEnabled();
            return;
        }

        var p = _projects.Get(id);
        if (p is null)
        {
            FocusSubtitle.Text = $"Missing project {id}";
            FocusEmptyPanel.Visibility = Visibility.Visible;
            FocusWorkPanel.Visibility = Visibility.Collapsed;
            FocusEmptyCopy.Text =
                $"Project {id} is no longer in firm.db. Return to Projects and pick another, or Import from Connect.";
            _focusProjectId = null;
            ApplyDockLabels();
            UpdateDockEnabled();
            return;
        }

        _focusProjectId = p.ProjectId;
        FocusEmptyPanel.Visibility = Visibility.Collapsed;
        FocusWorkPanel.Visibility = Visibility.Visible;
        FocusSubtitle.Text = $"{p.ProjectRef} · {p.Title}";
        FocusTitleBox.Text = p.Title;
        FocusRefBox.Text = p.ProjectRef;
        FocusStatusBox.Text = p.Status;
        FocusPhaseBox.Text = p.Phase;
        FocusNotesBox.Text = p.Notes;
        FocusClientIdBox.Text = p.ClientId;
        FocusJurisdictionBox.Text = p.Jurisdiction;
        FocusSiteAddressBox.Text = p.SiteAddress;
        FocusWorkTypeBox.Text = p.WorkType;
        FocusMetaText.Text =
            $"id={p.ProjectId}  publish={p.PublishState}  ·  Overview / Brief / Drawings / Documents / Fees / Site";
        FocusEngineText.Text = BbsEngineClient.DllPresent()
            ? $"bbs_engine.dll ready · {BbsEngineClient.DllPath()}"
            : "bbs_engine.dll missing — run build-engine.cmd then rebuild AStudio.";
        TaskProjectBox.Text = p.ProjectId;
        ShowFocusDomain(_focusDomain);
        UpdateDockEnabled();
    }

    void FocusTabOverview_Click(object sender, RoutedEventArgs e) => ShowFocusDomain(FocusDomain.Overview);
    void FocusTabBrief_Click(object sender, RoutedEventArgs e) => ShowFocusDomain(FocusDomain.Brief);
    void FocusTabFees_Click(object sender, RoutedEventArgs e) => ShowFocusDomain(FocusDomain.Fees);
    void FocusTabDrawings_Click(object sender, RoutedEventArgs e) => ShowFocusDomain(FocusDomain.Drawings);
    void FocusTabDocuments_Click(object sender, RoutedEventArgs e) => ShowFocusDomain(FocusDomain.Documents);
    void FocusTabSite_Click(object sender, RoutedEventArgs e) => ShowFocusDomain(FocusDomain.Site);

    void ShowFocusDomain(FocusDomain domain)
    {
        _focusDomain = domain;
        FocusOverviewPanel.Visibility = domain == FocusDomain.Overview ? Visibility.Visible : Visibility.Collapsed;
        FocusBriefPanel.Visibility = domain == FocusDomain.Brief ? Visibility.Visible : Visibility.Collapsed;
        FocusFeesPanel.Visibility = domain == FocusDomain.Fees ? Visibility.Visible : Visibility.Collapsed;
        FocusDrawingsPanel.Visibility = domain == FocusDomain.Drawings ? Visibility.Visible : Visibility.Collapsed;
        FocusDocumentsPanel.Visibility = domain == FocusDomain.Documents ? Visibility.Visible : Visibility.Collapsed;
        FocusSitePanel.Visibility = domain == FocusDomain.Site ? Visibility.Visible : Visibility.Collapsed;

        StyleNav(FocusTabOverviewBtn, domain == FocusDomain.Overview);
        StyleNav(FocusTabBriefBtn, domain == FocusDomain.Brief);
        StyleNav(FocusTabFeesBtn, domain == FocusDomain.Fees);
        StyleNav(FocusTabDrawingsBtn, domain == FocusDomain.Drawings);
        StyleNav(FocusTabDocumentsBtn, domain == FocusDomain.Documents);
        StyleNav(FocusTabSiteBtn, domain == FocusDomain.Site);

        switch (domain)
        {
            case FocusDomain.Overview:
                ReloadDecisions();
                ReloadCriticalNotes();
                break;
            case FocusDomain.Brief:
                ReloadRisks();
                break;
            case FocusDomain.Fees:
                ReloadFees();
                break;
            case FocusDomain.Drawings:
                ReloadDrawings();
                break;
            case FocusDomain.Documents:
                ReloadDocuments();
                break;
            case FocusDomain.Site:
                ReloadSite();
                break;
        }

        ApplyDockLabels();
        UpdateDockEnabled();
    }

    static List<LedgerRow> ToLedgerRows(IEnumerable<LocalLedgerItem> rows) =>
        rows.Select(r => new LedgerRow
        {
            ItemId = r.ItemId,
            Title = r.Title,
            Kind = r.Kind,
            Status = r.Status,
            PublishState = r.PublishState,
            Notes = r.Notes,
        }).ToList();

    void ReloadDecisions()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null) { DecisionsListView.ItemsSource = null; return; }
        var items = ToLedgerRows(_decisions.ListByProject(projectId));
        DecisionsListView.ItemsSource = items;
        _selectedDecisionId ??= items.FirstOrDefault()?.ItemId;
        SelectLedger(DecisionsListView, items, _selectedDecisionId);
    }

    void ReloadCriticalNotes()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null) { NotesListView.ItemsSource = null; return; }
        var items = ToLedgerRows(_criticalNotes.ListByProject(projectId));
        NotesListView.ItemsSource = items;
        _selectedNoteId ??= items.FirstOrDefault()?.ItemId;
        SelectLedger(NotesListView, items, _selectedNoteId);
    }

    void ReloadRisks()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null) { RisksListView.ItemsSource = null; return; }
        var items = ToLedgerRows(_risks.ListByProject(projectId));
        RisksListView.ItemsSource = items;
        _selectedRiskId ??= items.FirstOrDefault()?.ItemId;
        SelectLedger(RisksListView, items, _selectedRiskId);
    }

    void ReloadDocuments()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null) { DocumentsListView.ItemsSource = null; return; }
        var items = ToLedgerRows(_documents.ListByProject(projectId));
        DocumentsListView.ItemsSource = items;
        _selectedDocumentId ??= items.FirstOrDefault()?.ItemId;
        SelectLedger(DocumentsListView, items, _selectedDocumentId);
    }

    static void SelectLedger(ListView list, List<LedgerRow> items, string? id)
    {
        if (id is null) return;
        var match = items.FirstOrDefault(i => i.ItemId == id);
        if (match is not null) list.SelectedItem = match;
    }

    void DecisionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DecisionsListView.SelectedItem is LedgerRow row) _selectedDecisionId = row.ItemId;
    }

    void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NotesListView.SelectedItem is LedgerRow row) _selectedNoteId = row.ItemId;
    }

    void RisksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RisksListView.SelectedItem is LedgerRow row) _selectedRiskId = row.ItemId;
    }

    void DocumentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentsListView.SelectedItem is LedgerRow row) _selectedDocumentId = row.ItemId;
    }

    void FeesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FeesListView.SelectedItem is FeeRow row) _selectedFeeId = row.FeeId;
    }

    void DrawingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DrawingsListView.SelectedItem is DrawingRow row) _selectedDrawingId = row.DrawingId;
    }

    void SiteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SiteListView.SelectedItem is LedgerRow row) _selectedDeliveryId = row.ItemId;
    }

    void ClientsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClientsListView.SelectedItem is ClientRow row)
        {
            _selectedClientId = row.ClientId;
            var c = _clients.Get(row.ClientId);
            if (c is null) return;
            ClientNameBox.Text = c.Name;
            ClientContactBox.Text = c.Contact;
            ClientEmailBox.Text = c.Email;
            ClientNotesBox.Text = c.Notes;
        }
    }

    void TasksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TasksListView.SelectedItem is TaskRow row)
            _selectedTaskId = row.TaskId;
    }

    void SiteFacetAll_Click(object sender, RoutedEventArgs e) { _siteFacet = null; ReloadSite(); }
    void SiteFacetVisit_Click(object sender, RoutedEventArgs e)
    {
        _siteFacet = "VISIT";
        DeliveryKindBox.Text = "VISIT";
        ReloadSite();
    }
    void SiteFacetSnag_Click(object sender, RoutedEventArgs e)
    {
        _siteFacet = "SNAG";
        DeliveryKindBox.Text = "SNAG";
        ReloadSite();
    }
    void SiteFacetProgress_Click(object sender, RoutedEventArgs e)
    {
        _siteFacet = "PROGRESS";
        DeliveryKindBox.Text = "PROGRESS";
        ReloadSite();
    }

    void ReloadFees()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            FeesListView.ItemsSource = null;
            FeeListText.Text = "(no project)";
            return;
        }
        var rows = _fees.ListByProject(projectId);
        var items = rows.Select(r => new FeeRow
        {
            FeeId = r.FeeId,
            Title = r.Title,
            Amount = MoneyPaise.FormatInr(r.AmountPaise),
            Status = r.Status,
            PublishState = r.PublishState,
        }).ToList();
        FeesListView.ItemsSource = items;
        FeeListText.Text = items.Count == 0 ? "(no fees)" : $"{items.Count} fee(s)";
        _selectedFeeId ??= items.FirstOrDefault()?.FeeId;
        if (_selectedFeeId is not null)
        {
            var match = items.FirstOrDefault(i => i.FeeId == _selectedFeeId);
            if (match is not null) FeesListView.SelectedItem = match;
        }
    }

    void ReloadDrawings()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            DrawingsListView.ItemsSource = null;
            DrawingListText.Text = "(no project)";
            return;
        }
        var rows = _drawings.ListByProject(projectId);
        var items = rows.Select(r => new DrawingRow
        {
            DrawingId = r.DrawingId,
            Number = r.Number,
            Title = r.Title,
            Rev = r.Rev,
            Status = r.Status,
            PublishState = r.PublishState,
            HashShort = string.IsNullOrEmpty(r.ContentHash)
                ? "-"
                : r.ContentHash[..Math.Min(8, r.ContentHash.Length)],
        }).ToList();
        DrawingsListView.ItemsSource = items;
        DrawingListText.Text = items.Count == 0 ? "(no drawings)" : $"{items.Count} drawing(s)";
        _selectedDrawingId ??= items.FirstOrDefault()?.DrawingId;
        if (_selectedDrawingId is not null)
        {
            var match = items.FirstOrDefault(i => i.DrawingId == _selectedDrawingId);
            if (match is not null) DrawingsListView.SelectedItem = match;
        }
    }

    void ReloadSite()
    {
        var projectId = ResolveFocusProjectId();
        StyleNav(SiteFacetAllBtn, _siteFacet is null);
        StyleNav(SiteFacetVisitBtn, _siteFacet == "VISIT");
        StyleNav(SiteFacetSnagBtn, _siteFacet == "SNAG");
        StyleNav(SiteFacetProgressBtn, _siteFacet == "PROGRESS");
        if (projectId is null)
        {
            SiteListView.ItemsSource = null;
            DeliveryListText.Text = "(no project)";
            return;
        }
        IEnumerable<LocalDeliveryItem> rows = _delivery.ListByProject(projectId);
        if (_siteFacet is not null)
        {
            rows = rows.Where(r =>
                string.Equals(r.Kind, _siteFacet, StringComparison.OrdinalIgnoreCase) ||
                (_siteFacet == "VISIT" && r.Kind.Contains("VISIT", StringComparison.OrdinalIgnoreCase)) ||
                (_siteFacet == "SNAG" && r.Kind.Contains("SNAG", StringComparison.OrdinalIgnoreCase)) ||
                (_siteFacet == "PROGRESS" &&
                 (r.Kind.Contains("PROGRESS", StringComparison.OrdinalIgnoreCase) ||
                  r.Kind.Contains("INSTRUCTION", StringComparison.OrdinalIgnoreCase))));
        }
        var items = rows.Select(r => new LedgerRow
        {
            ItemId = r.ItemId,
            Title = r.Title,
            Kind = r.Kind,
            Status = r.Status,
            PublishState = r.PublishState,
            Notes = r.Notes,
        }).ToList();
        SiteListView.ItemsSource = items;
        DeliveryListText.Text = items.Count == 0 ? "(no site items)" : $"{items.Count} item(s)";
        _selectedDeliveryId ??= items.FirstOrDefault()?.ItemId;
        SelectLedger(SiteListView, items, _selectedDeliveryId);
    }

    void ReloadDelivery() => ReloadSite();

    void SavePortfolioProject()
    {
        var title = ProjectTitleBox.Text?.Trim() ?? "";
        var projectRef = ProjectRefBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(projectRef))
        {
            TrayText.Text = "Title and ref required.";
            return;
        }
        var id = Guid.NewGuid().ToString("N")[..12];
        var phase = ProjectPhaseBox.Text?.Trim() ?? "";
        _projects.Upsert(id, projectRef, title, "ACTIVE", phase, "", "LOCAL");
        _selectedProjectId = id;
        ProjectTitleBox.Text = "";
        ProjectRefBox.Text = "";
        ProjectPhaseBox.Text = "";
        ReloadProjects();
        TrayText.Text = $"Saved project {id}";
    }

    bool SaveFocusProject(bool quiet = false)
    {
        var id = ResolveFocusProjectId();
        if (id is null)
        {
            if (!quiet) TrayText.Text = "No project in focus — open one from Portfolio.";
            return false;
        }
        var existing = _projects.Get(id);
        if (existing is null)
        {
            if (!quiet) TrayText.Text = "Project not found.";
            return false;
        }
        var title = FocusTitleBox.Text?.Trim() ?? "";
        var projectRef = FocusRefBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(projectRef))
        {
            if (!quiet) TrayText.Text = "Title and ref required.";
            return false;
        }
        _projects.Upsert(
            id,
            projectRef,
            title,
            string.IsNullOrWhiteSpace(FocusStatusBox.Text) ? "ACTIVE" : FocusStatusBox.Text.Trim(),
            FocusPhaseBox.Text?.Trim() ?? "",
            FocusNotesBox.Text ?? "",
            existing.PublishState,
            FocusClientIdBox.Text?.Trim() ?? "",
            FocusJurisdictionBox.Text?.Trim() ?? "",
            FocusSiteAddressBox.Text?.Trim() ?? "",
            string.IsNullOrWhiteSpace(FocusWorkTypeBox.Text) ? "ARCHITECTURE" : FocusWorkTypeBox.Text.Trim());
        TrySaveRisk(quiet: true);
        LoadFocusForm();
        if (!quiet) TrayText.Text = $"Saved brief · {projectRef} ({existing.PublishState})";
        return true;
    }

    bool TrySaveRisk(bool quiet = false)
    {
        var projectId = ResolveFocusProjectId();
        var title = RiskTitleBox.Text?.Trim() ?? "";
        if (projectId is null || string.IsNullOrEmpty(title)) return false;
        var id = Guid.NewGuid().ToString("N")[..12];
        var kind = string.IsNullOrWhiteSpace(RiskKindBox.Text) ? "RISK" : RiskKindBox.Text.Trim().ToUpperInvariant();
        var status = string.IsNullOrWhiteSpace(RiskStatusBox.Text) ? "OPEN" : RiskStatusBox.Text.Trim().ToUpperInvariant();
        _risks.Upsert(id, projectId, title, kind, status, RiskNotesBox.Text ?? "", "LOCAL");
        _selectedRiskId = id;
        RiskTitleBox.Text = "";
        RiskKindBox.Text = "";
        RiskStatusBox.Text = "";
        RiskNotesBox.Text = "";
        ReloadRisks();
        if (!quiet) TrayText.Text = $"Saved R&O · {id}";
        return true;
    }

    void SaveOverviewLedger()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            TrayText.Text = "No project in focus.";
            return;
        }
        var decTitle = DecisionTitleBox.Text?.Trim() ?? "";
        var noteTitle = NoteTitleBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(decTitle) && string.IsNullOrEmpty(noteTitle))
        {
            TrayText.Text = "Enter a decision or critical note title.";
            return;
        }
        if (!string.IsNullOrEmpty(decTitle))
        {
            var id = Guid.NewGuid().ToString("N")[..12];
            var kind = string.IsNullOrWhiteSpace(DecisionKindBox.Text)
                ? "MAJOR"
                : DecisionKindBox.Text.Trim().ToUpperInvariant();
            var status = string.IsNullOrWhiteSpace(DecisionStatusBox.Text)
                ? "OPEN"
                : DecisionStatusBox.Text.Trim().ToUpperInvariant();
            _decisions.Upsert(id, projectId, decTitle, kind, status, DecisionNotesBox.Text ?? "", "LOCAL");
            _selectedDecisionId = id;
            DecisionTitleBox.Text = "";
            DecisionKindBox.Text = "";
            DecisionStatusBox.Text = "";
            DecisionNotesBox.Text = "";
            ReloadDecisions();
            TrayText.Text = $"Saved decision · {id}";
        }
        if (!string.IsNullOrEmpty(noteTitle))
        {
            var id = Guid.NewGuid().ToString("N")[..12];
            var kind = string.IsNullOrWhiteSpace(NoteKindBox.Text)
                ? "SITE"
                : NoteKindBox.Text.Trim().ToUpperInvariant();
            var status = string.IsNullOrWhiteSpace(NoteStatusBox.Text)
                ? "OPEN"
                : NoteStatusBox.Text.Trim().ToUpperInvariant();
            _criticalNotes.Upsert(id, projectId, noteTitle, kind, status, NoteNotesBox.Text ?? "", "LOCAL");
            _selectedNoteId = id;
            NoteTitleBox.Text = "";
            NoteKindBox.Text = "";
            NoteStatusBox.Text = "";
            NoteNotesBox.Text = "";
            ReloadCriticalNotes();
            TrayText.Text = $"Saved critical note · {id}";
        }
    }

    async Task PublishOverviewAsync()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            TrayText.Text = "No project in focus.";
            return;
        }
        LocalLedgerItem? row = null;
        if (_selectedDecisionId is not null)
            row = _decisions.Get(_selectedDecisionId);
        row ??= _decisions.ListByProject(projectId).FirstOrDefault();
        if (row is null)
        {
            TrayText.Text = "Save a decision first (approvalState).";
            return;
        }
        try
        {
            _bridge.EnqueueMeta("approvalState", row.ItemId, new Dictionary<string, object?>
            {
                ["itemId"] = row.ItemId,
                ["projectId"] = row.ProjectId,
                ["title"] = row.Title,
                ["impact"] = row.Kind,
                ["state"] = row.Status,
                ["updatedAt"] = DateTime.UtcNow.ToString("O"),
            });
            // Critical notes ride presence (allow-listed) when selected/open.
            var note = _selectedNoteId is not null
                ? _criticalNotes.Get(_selectedNoteId)
                : _criticalNotes.ListByProject(projectId).FirstOrDefault();
            if (note is not null)
            {
                _bridge.EnqueueMeta("presence", note.ItemId, new Dictionary<string, object?>
                {
                    ["kind"] = "criticalNote",
                    ["itemId"] = note.ItemId,
                    ["projectId"] = note.ProjectId,
                    ["title"] = note.Title,
                    ["status"] = note.Status,
                    ["updatedAt"] = DateTime.UtcNow.ToString("O"),
                });
                _criticalNotes.Upsert(note.ItemId, note.ProjectId, note.Title, note.Kind, note.Status, note.Notes, "QUEUED");
            }
            _decisions.Upsert(row.ItemId, row.ProjectId, row.Title, row.Kind, row.Status, row.Notes, "QUEUED");
            var result = await _bridge.FlushAsync();
            if (result.SkippedReason is not null)
            {
                TrayText.Text = $"Queued approvalState; flush skipped={result.SkippedReason}";
                LogText.Text = $"Flush skipped={result.SkippedReason}";
            }
            else
            {
                _decisions.Upsert(row.ItemId, row.ProjectId, row.Title, row.Kind, row.Status, row.Notes, "PUBLISHED");
                if (note is not null)
                    _criticalNotes.Upsert(note.ItemId, note.ProjectId, note.Title, note.Kind, note.Status, note.Notes, "PUBLISHED");
                TrayText.Text = $"Published approval · {row.Title} · metaSent={result.MetaSent}";
            }
            ReloadDecisions();
            ReloadCriticalNotes();
        }
        catch (Exception ex)
        {
            TrayText.Text = $"Publish failed: {ex.Message}";
        }
    }

    void SaveDocument()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            TrayText.Text = "No project in focus.";
            return;
        }
        var title = DocTitleBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(title))
        {
            TrayText.Text = "Document title required.";
            return;
        }
        var id = Guid.NewGuid().ToString("N")[..12];
        var kind = string.IsNullOrWhiteSpace(DocKindBox.Text) ? "OTHER" : DocKindBox.Text.Trim().ToUpperInvariant();
        var status = string.IsNullOrWhiteSpace(DocStatusBox.Text) ? "DRAFT" : DocStatusBox.Text.Trim().ToUpperInvariant();
        _documents.Upsert(id, projectId, title, kind, status, DocNotesBox.Text ?? "", "LOCAL");
        _selectedDocumentId = id;
        DocTitleBox.Text = "";
        DocKindBox.Text = "";
        DocStatusBox.Text = "";
        DocNotesBox.Text = "";
        ReloadDocuments();
        TrayText.Text = $"Saved document · {id}";
    }

    async Task PublishDocumentAsync()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            TrayText.Text = "No project in focus.";
            return;
        }
        var row = _selectedDocumentId is not null
            ? _documents.Get(_selectedDocumentId)
            : _documents.ListByProject(projectId).FirstOrDefault();
        if (row is null)
        {
            TrayText.Text = "Save a document first.";
            return;
        }
        try
        {
            // No documentRegister on hub allow-list yet — presence carries the register row.
            _bridge.EnqueueMeta("presence", row.ItemId, new Dictionary<string, object?>
            {
                ["kind"] = "documentRegister",
                ["itemId"] = row.ItemId,
                ["projectId"] = row.ProjectId,
                ["title"] = row.Title,
                ["docKind"] = row.Kind,
                ["status"] = row.Status,
                ["updatedAt"] = DateTime.UtcNow.ToString("O"),
            });
            _documents.Upsert(row.ItemId, row.ProjectId, row.Title, row.Kind, row.Status, row.Notes, "QUEUED");
            var result = await _bridge.FlushAsync();
            if (result.SkippedReason is not null)
            {
                TrayText.Text = $"Queued document presence; flush skipped={result.SkippedReason}";
            }
            else
            {
                _documents.Upsert(row.ItemId, row.ProjectId, row.Title, row.Kind, row.Status, row.Notes, "PUBLISHED");
                TrayText.Text = $"Published document · {row.Title} · metaSent={result.MetaSent}";
            }
            ReloadDocuments();
        }
        catch (Exception ex)
        {
            TrayText.Text = $"Publish failed: {ex.Message}";
        }
    }

    async Task PublishProjectStatusAsync()
    {
        // From Focus brief, persist edits before enqueue so hub gets current fields.
        if (_stage == StageId.ProjectFocus && _focusDomain == FocusDomain.Brief)
        {
            if (!SaveFocusProject(quiet: true))
            {
                TrayText.Text = "Save brief first — no project or title/ref missing.";
                return;
            }
        }

        var id = ResolveFocusProjectId();
        if (id is null)
        {
            TrayText.Text = "No project to publish — select one in Projects.";
            return;
        }
        var p = _projects.Get(id);
        if (p is null)
        {
            TrayText.Text = "Project not found.";
            return;
        }
        try
        {
            var riskCount = _risks.ListByProject(p.ProjectId).Count;
            _bridge.EnqueueMeta("projectStatus", p.ProjectId, new Dictionary<string, object?>
            {
                ["projectId"] = p.ProjectId,
                ["ref"] = p.ProjectRef,
                ["title"] = p.Title,
                ["status"] = p.Status,
                ["phase"] = p.Phase,
                ["clientId"] = p.ClientId,
                ["jurisdiction"] = p.Jurisdiction,
                ["siteAddress"] = p.SiteAddress,
                ["workType"] = p.WorkType,
                ["riskCount"] = riskCount,
                ["updatedAt"] = DateTime.UtcNow.ToString("O"),
            });
            _projects.SetPublishState(p.ProjectId, "QUEUED");
            var result = await _bridge.FlushAsync();
            if (result.SkippedReason is not null)
            {
                TrayText.Text =
                    $"Queued projectStatus for {p.ProjectRef}; flush skipped={result.SkippedReason} — activate on Practice if needed.";
                LogText.Text = $"Flush skipped={result.SkippedReason}";
            }
            else
            {
                _projects.SetPublishState(p.ProjectId, "PUBLISHED");
                TrayText.Text = $"Published status · {p.ProjectRef} · metaSent={result.MetaSent}";
                LogText.Text = $"projectStatus OK · {p.ProjectId}";
            }
            if (_stage == StageId.Projects) ReloadProjects();
            if (_stage == StageId.ProjectFocus) LoadFocusForm();
        }
        catch (Exception ex)
        {
            TrayText.Text = $"Publish failed: {ex.Message}";
        }
    }

    void SaveFee()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            TrayText.Text = "No project in focus.";
            return;
        }
        var title = FeeTitleBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(title))
        {
            TrayText.Text = "Fee title required.";
            return;
        }
        if (!MoneyPaise.TryParseRupees(FeeAmountBox.Text, out var paise))
        {
            TrayText.Text = "Amount (INR) required — e.g. 125000.";
            return;
        }
        var id = Guid.NewGuid().ToString("N")[..12];
        var status = string.IsNullOrWhiteSpace(FeeStatusBox.Text) ? "DRAFT" : FeeStatusBox.Text.Trim().ToUpperInvariant();
        _fees.Upsert(id, projectId, title, paise, status, FeeNotesBox.Text ?? "", "LOCAL");
        _selectedFeeId = id;
        FeeTitleBox.Text = "";
        FeeAmountBox.Text = "";
        FeeStatusBox.Text = "";
        FeeNotesBox.Text = "";
        ReloadFees();
        TrayText.Text = $"Saved fee {id} · {MoneyPaise.FormatInr(paise)}";
    }

    async Task PublishFeeAsync()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            TrayText.Text = "No project in focus.";
            return;
        }
        // Prefer form fields if filled; else publish selected row.
        LocalFee? row = null;
        var title = FeeTitleBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(title) && MoneyPaise.TryParseRupees(FeeAmountBox.Text, out var paise))
        {
            var id = Guid.NewGuid().ToString("N")[..12];
            var status = string.IsNullOrWhiteSpace(FeeStatusBox.Text) ? "DRAFT" : FeeStatusBox.Text.Trim().ToUpperInvariant();
            _fees.Upsert(id, projectId, title, paise, status, FeeNotesBox.Text ?? "", "LOCAL");
            _selectedFeeId = id;
            FeeTitleBox.Text = "";
            FeeAmountBox.Text = "";
            FeeStatusBox.Text = "";
            FeeNotesBox.Text = "";
            row = _fees.Get(id);
        }
        else if (_selectedFeeId is not null)
        {
            row = _fees.Get(_selectedFeeId);
        }
        else
        {
            row = _fees.ListByProject(projectId).FirstOrDefault();
        }

        if (row is null)
        {
            TrayText.Text = "Save a fee first (title + amount).";
            return;
        }

        try
        {
            _bridge.EnqueueMeta("invoiceStatus", row.FeeId, new Dictionary<string, object?>
            {
                ["feeId"] = row.FeeId,
                ["projectId"] = row.ProjectId,
                ["title"] = row.Title,
                ["amountPaise"] = row.AmountPaise,
                ["status"] = row.Status,
                ["updatedAt"] = DateTime.UtcNow.ToString("O"),
            });
            _fees.Upsert(row.FeeId, row.ProjectId, row.Title, row.AmountPaise, row.Status, row.Notes, "QUEUED");
            var result = await _bridge.FlushAsync();
            if (result.SkippedReason is not null)
            {
                TrayText.Text =
                    $"Queued invoiceStatus; flush skipped={result.SkippedReason} — Activate in AORMS Connect first.";
                LogText.Text = $"Flush skipped={result.SkippedReason}";
            }
            else
            {
                _fees.Upsert(row.FeeId, row.ProjectId, row.Title, row.AmountPaise, row.Status, row.Notes, "PUBLISHED");
                TrayText.Text = $"Published invoice · {MoneyPaise.FormatInr(row.AmountPaise)} · metaSent={result.MetaSent}";
                LogText.Text = $"invoiceStatus OK · {row.FeeId}";
            }
            ReloadFees();
        }
        catch (Exception ex)
        {
            TrayText.Text = $"Publish failed: {ex.Message}";
        }
    }

    void SaveDrawing()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            TrayText.Text = "No project in focus.";
            return;
        }
        var number = DrawingNumberBox.Text?.Trim() ?? "";
        var title = DrawingTitleBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(number) || string.IsNullOrEmpty(title))
        {
            TrayText.Text = "Drawing number and title required.";
            return;
        }
        var id = Guid.NewGuid().ToString("N")[..12];
        var rev = string.IsNullOrWhiteSpace(DrawingRevBox.Text) ? "A" : DrawingRevBox.Text.Trim();
        var status = string.IsNullOrWhiteSpace(DrawingStatusBox.Text)
            ? "WIP"
            : DrawingStatusBox.Text.Trim().ToUpperInvariant();
        var path = DrawingPathBox.Text?.Trim() ?? "";
        var hash = ContentHash.Sha256File(path) ?? "";
        _drawings.Upsert(id, projectId, number, title, rev, status, DrawingNotesBox.Text ?? "", "LOCAL", path, hash);
        _selectedDrawingId = id;
        DrawingNumberBox.Text = "";
        DrawingTitleBox.Text = "";
        DrawingRevBox.Text = "";
        DrawingStatusBox.Text = "";
        DrawingPathBox.Text = "";
        DrawingNotesBox.Text = "";
        ReloadDrawings();
        TrayText.Text = string.IsNullOrEmpty(hash)
            ? $"Saved drawing {number}-Rev{rev}"
            : $"Saved drawing {number}-Rev{rev} · sha256={hash[..8]}…";
    }

    async Task PublishDrawingAsync()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            TrayText.Text = "No project in focus.";
            return;
        }

        var row = ResolveDrawingRow(projectId, preferReady: true);
        if (row is null)
        {
            TrayText.Text = "Save a drawing first (number + title).";
            return;
        }

        try
        {
            _bridge.EnqueueMeta("drawingRegister", row.DrawingId, new Dictionary<string, object?>
            {
                ["drawingId"] = row.DrawingId,
                ["projectId"] = row.ProjectId,
                ["number"] = row.Number,
                ["title"] = row.Title,
                ["rev"] = row.Rev,
                ["status"] = row.Status,
                ["contentHash"] = row.ContentHash,
                ["updatedAt"] = DateTime.UtcNow.ToString("O"),
            });
            _drawings.Upsert(row.DrawingId, row.ProjectId, row.Number, row.Title, row.Rev, row.Status, row.Notes, "QUEUED",
                row.LocalPath, row.ContentHash);
            var result = await _bridge.FlushAsync();
            if (result.SkippedReason is not null)
            {
                TrayText.Text =
                    $"Queued drawingRegister; flush skipped={result.SkippedReason} — Activate in AORMS Connect first.";
                LogText.Text = $"Flush skipped={result.SkippedReason}";
            }
            else
            {
                _drawings.Upsert(row.DrawingId, row.ProjectId, row.Number, row.Title, row.Rev, row.Status, row.Notes, "PUBLISHED",
                    row.LocalPath, row.ContentHash);
                TrayText.Text = $"Published register · {row.Number}-Rev{row.Rev} · metaSent={result.MetaSent}";
                LogText.Text = $"drawingRegister OK · {row.DrawingId}";
            }
            ReloadDrawings();
        }
        catch (Exception ex)
        {
            TrayText.Text = $"Publish failed: {ex.Message}";
        }
    }

    /// <summary>S3e — enqueue allow-listed drawing artifact (JSON envelope + sha256; binary upload later).</summary>
    async void QueueDrawingArtifact_Click(object sender, RoutedEventArgs e)
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            TrayText.Text = "No project in focus.";
            return;
        }

        var row = ResolveDrawingRow(projectId, preferReady: false);
        if (row is null)
        {
            TrayText.Text = "Save a drawing first (number + title).";
            return;
        }

        var path = DrawingPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(path)) path = row.LocalPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            TrayText.Text = "Set a valid local file path, Save drawing, then Queue artifact.";
            return;
        }

        var hash = ContentHash.Sha256File(path);
        if (string.IsNullOrEmpty(hash))
        {
            TrayText.Text = "Could not hash file.";
            return;
        }

        var storageKey = $"drawings/{row.ProjectId}/{row.DrawingId}/{row.Number}-rev{row.Rev}";
        try
        {
            _drawings.Upsert(row.DrawingId, row.ProjectId, row.Number, row.Title, row.Rev, row.Status, row.Notes,
                row.PublishState, path, hash);
            _bridge.EnqueueArtifact(
                "drawing",
                row.DrawingId,
                new Dictionary<string, object?>
                {
                    ["drawingId"] = row.DrawingId,
                    ["projectId"] = row.ProjectId,
                    ["number"] = row.Number,
                    ["title"] = row.Title,
                    ["rev"] = row.Rev,
                    ["status"] = row.Status,
                    ["localPath"] = path,
                    ["storageKey"] = storageKey,
                    ["contentHash"] = hash,
                    ["updatedAt"] = DateTime.UtcNow.ToString("O"),
                },
                contentHash: hash,
                storageKey: storageKey);
            // Also keep register meta in sync for portals.
            _bridge.EnqueueMeta("drawingRegister", row.DrawingId, new Dictionary<string, object?>
            {
                ["drawingId"] = row.DrawingId,
                ["projectId"] = row.ProjectId,
                ["number"] = row.Number,
                ["title"] = row.Title,
                ["rev"] = row.Rev,
                ["status"] = row.Status,
                ["contentHash"] = hash,
                ["storageKey"] = storageKey,
                ["updatedAt"] = DateTime.UtcNow.ToString("O"),
            });
            _drawings.Upsert(row.DrawingId, row.ProjectId, row.Number, row.Title, row.Rev, row.Status, row.Notes, "QUEUED",
                path, hash);
            var result = await _bridge.FlushAsync();
            if (result.SkippedReason is not null)
            {
                TrayText.Text =
                    $"Queued drawing artifact; flush skipped={result.SkippedReason} — Activate in AORMS Connect first.";
                LogText.Text = $"Flush skipped={result.SkippedReason} · sha256={hash[..8]}…";
            }
            else
            {
                _drawings.Upsert(row.DrawingId, row.ProjectId, row.Number, row.Title, row.Rev, row.Status, row.Notes, "PUBLISHED",
                    path, hash);
                TrayText.Text =
                    $"Artifact ingest · {row.Number}-Rev{row.Rev} · meta={result.MetaSent} art={result.ArtifactsSent}";
                LogText.Text = $"drawing ingest OK · {storageKey} · sha256={hash[..12]}…";
            }
            DrawingPathBox.Text = "";
            ReloadDrawings();
        }
        catch (Exception ex)
        {
            TrayText.Text = $"Artifact queue failed: {ex.Message}";
        }
    }

    LocalDrawing? ResolveDrawingRow(string projectId, bool preferReady)
    {
        var number = DrawingNumberBox.Text?.Trim() ?? "";
        var title = DrawingTitleBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(number) && !string.IsNullOrEmpty(title))
        {
            var id = Guid.NewGuid().ToString("N")[..12];
            var rev = string.IsNullOrWhiteSpace(DrawingRevBox.Text) ? "A" : DrawingRevBox.Text.Trim();
            var status = string.IsNullOrWhiteSpace(DrawingStatusBox.Text)
                ? (preferReady ? "READY" : "WIP")
                : DrawingStatusBox.Text.Trim().ToUpperInvariant();
            var path = DrawingPathBox.Text?.Trim() ?? "";
            var hash = ContentHash.Sha256File(path) ?? "";
            _drawings.Upsert(id, projectId, number, title, rev, status, DrawingNotesBox.Text ?? "", "LOCAL", path, hash);
            _selectedDrawingId = id;
            DrawingNumberBox.Text = "";
            DrawingTitleBox.Text = "";
            DrawingRevBox.Text = "";
            DrawingStatusBox.Text = "";
            DrawingPathBox.Text = "";
            DrawingNotesBox.Text = "";
            return _drawings.Get(id);
        }
        if (_selectedDrawingId is not null)
            return _drawings.Get(_selectedDrawingId);
        return _drawings.ListByProject(projectId).FirstOrDefault();
    }

    void SaveDelivery()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            TrayText.Text = "No project in focus.";
            return;
        }
        var title = DeliveryTitleBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(title))
        {
            TrayText.Text = "Delivery title required.";
            return;
        }
        var id = Guid.NewGuid().ToString("N")[..12];
        var kind = string.IsNullOrWhiteSpace(DeliveryKindBox.Text)
            ? "PROGRESS"
            : DeliveryKindBox.Text.Trim().ToUpperInvariant();
        var status = string.IsNullOrWhiteSpace(DeliveryStatusBox.Text)
            ? "OPEN"
            : DeliveryStatusBox.Text.Trim().ToUpperInvariant();
        _delivery.Upsert(id, projectId, kind, title, status, DeliveryNotesBox.Text ?? "", "LOCAL");
        _selectedDeliveryId = id;
        DeliveryKindBox.Text = "";
        DeliveryTitleBox.Text = "";
        DeliveryStatusBox.Text = "";
        DeliveryNotesBox.Text = "";
        ReloadDelivery();
        TrayText.Text = $"Saved delivery {kind} · {id}";
    }

    async Task PublishDeliveryAsync()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            TrayText.Text = "No project in focus.";
            return;
        }

        LocalDeliveryItem? row = null;
        var title = DeliveryTitleBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(title))
        {
            var id = Guid.NewGuid().ToString("N")[..12];
            var kind = string.IsNullOrWhiteSpace(DeliveryKindBox.Text)
                ? "PROGRESS"
                : DeliveryKindBox.Text.Trim().ToUpperInvariant();
            var status = string.IsNullOrWhiteSpace(DeliveryStatusBox.Text)
                ? "OPEN"
                : DeliveryStatusBox.Text.Trim().ToUpperInvariant();
            _delivery.Upsert(id, projectId, kind, title, status, DeliveryNotesBox.Text ?? "", "LOCAL");
            _selectedDeliveryId = id;
            DeliveryKindBox.Text = "";
            DeliveryTitleBox.Text = "";
            DeliveryStatusBox.Text = "";
            DeliveryNotesBox.Text = "";
            row = _delivery.Get(id);
        }
        else if (_selectedDeliveryId is not null)
        {
            row = _delivery.Get(_selectedDeliveryId);
        }
        else
        {
            row = _delivery.ListByProject(projectId).FirstOrDefault();
        }

        if (row is null)
        {
            TrayText.Text = "Save a delivery item first.";
            return;
        }

        try
        {
            _bridge.EnqueueMeta("phaseProgress", row.ItemId, new Dictionary<string, object?>
            {
                ["itemId"] = row.ItemId,
                ["projectId"] = row.ProjectId,
                ["kind"] = row.Kind,
                ["title"] = row.Title,
                ["status"] = row.Status,
                ["updatedAt"] = DateTime.UtcNow.ToString("O"),
            });
            _delivery.Upsert(row.ItemId, row.ProjectId, row.Kind, row.Title, row.Status, row.Notes, "QUEUED");
            var result = await _bridge.FlushAsync();
            if (result.SkippedReason is not null)
            {
                TrayText.Text =
                    $"Queued phaseProgress; flush skipped={result.SkippedReason} — Activate in AORMS Connect first.";
                LogText.Text = $"Flush skipped={result.SkippedReason}";
            }
            else
            {
                _delivery.Upsert(row.ItemId, row.ProjectId, row.Kind, row.Title, row.Status, row.Notes, "PUBLISHED");
                TrayText.Text = $"Published progress · {row.Kind} · metaSent={result.MetaSent}";
                LogText.Text = $"phaseProgress OK · {row.ItemId}";
            }
            ReloadDelivery();
        }
        catch (Exception ex)
        {
            TrayText.Text = $"Publish failed: {ex.Message}";
        }
    }

    void OpenSelectedFocus_Click(object sender, RoutedEventArgs e)
    {
        var rows = _projects.List();
        if (rows.Count == 0)
        {
            TrayText.Text = "No projects yet — save one or Import from Connect.";
            return;
        }
        _focusProjectId = _selectedProjectId ?? rows[0].ProjectId;
        _selectedProjectId = _focusProjectId;
        ShowStage(StageId.ProjectFocus);
        TrayText.Text = $"Focus · {_focusProjectId}";
    }

    /// <summary>S2d — in-process bbs_engine P/Invoke smoke (sample column).</summary>
    void EngineSmoke_Click(object sender, RoutedEventArgs e)
    {
        if (!BbsEngineClient.DllPresent())
        {
            var msg =
                "bbs_engine.dll not beside AStudio.exe. Run build-engine.cmd (MSVC), then rebuild AStudio so the DLL copies to output.";
            FocusEngineText.Text = msg;
            TrayText.Text = "Engine DLL missing.";
            LogText.Text = msg;
            return;
        }

        try
        {
            var res = BbsEngineClient.SmokeColumn();
            var summary = BbsEngineClient.FormatSmokeSummary(res);
            FocusEngineText.Text = summary;
            TrayText.Text = res.Ok ? "Engine smoke OK" : "Engine smoke failed";
            LogText.Text = summary;
        }
        catch (DllNotFoundException ex)
        {
            FocusEngineText.Text = $"P/Invoke load failed: {ex.Message}";
            TrayText.Text = "Engine load failed.";
            LogText.Text = ex.ToString();
        }
        catch (Exception ex)
        {
            FocusEngineText.Text = $"Engine smoke exception: {ex.Message}";
            TrayText.Text = "Engine smoke failed.";
            LogText.Text = ex.ToString();
        }
    }

    /// <summary>S2e — launch AQC Estimation (technical calc stays out of AStudio process).</summary>
    void OpenAqcEstimation_Click(object sender, RoutedEventArgs e) =>
        LaunchSuiteApp("AQC-Estimation", "AQC Estimation", "ESTI_AQC_ESTIMATION_EXE");

    void OpenAqcBbs_Click(object sender, RoutedEventArgs e) =>
        LaunchSuiteApp("AQC-BBS", "AQC BBS", "ESTI_AQC_BBS_EXE");

    void LaunchSuiteApp(string folderHint, string productLabel, string envOverride)
    {
        var sessionPath = ConnectSession.DefaultPath();
        var candidates = new List<string>();
        var envPath = Environment.GetEnvironmentVariable(envOverride);
        if (!string.IsNullOrWhiteSpace(envPath)) candidates.Add(envPath.Trim());
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        candidates.Add(Path.Combine(local, "Programs", folderHint, $"{folderHint}.exe"));
        candidates.Add(Path.Combine(pf, folderHint, $"{folderHint}.exe"));
        // Dev smoke: sibling thin shells + AQC vendor pin under Repos/.
        var repos = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        candidates.Add(Path.Combine(repos, folderHint, "src", "bin", "x64", "Release",
            "net8.0-windows10.0.19041.0", $"{folderHint}.exe"));
        var vendorBbs = Path.Combine(repos, "AStudio", "vendor", "AQC", "BBSDesktop");
        candidates.Add(Path.Combine(vendorBbs, "AQC.Estimation", "bin", "Release", "net8.0", "AQC.Estimation.exe"));
        candidates.Add(Path.Combine(vendorBbs, "BBSApp", "bin", "x64", "Release",
            "net8.0-windows10.0.19041.0", "AQCCore.exe"));
        candidates.Add(Path.Combine(repos, "AQC", "BBSDesktop", "BBSApp", "bin", "x64", "Release",
            "net8.0-windows10.0.19041.0", "AQCCore.exe"));

        foreach (var path in candidates.Where(File.Exists))
        {
            var args = File.Exists(sessionPath)
                ? $"{ConnectSession.FlagConnectSession} \"{sessionPath}\""
                : "";
            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                Arguments = args,
            });
            var focus = ResolveFocusProjectId();
            TrayText.Text = focus is null
                ? $"Launched {productLabel}"
                : $"Launched {productLabel} · focus project {focus}";
            LogText.Text = path;
            return;
        }

        TrayText.Text =
            $"{productLabel} not installed. Set {envOverride}, install via Connect Downloads, or build AQC.";
        LogText.Text = $"Tried: {string.Join(" | ", candidates.Take(4))}…";
    }

    void ImportCatalog_Click(object sender, RoutedEventArgs e)
    {
        var catalog = ConnectCatalog.List();
        if (catalog.Count == 0)
        {
            var note =
                "Connect catalog empty — open AORMS Connect and sync projects, then retry. " +
                @"Expected: %LocalAppData%\AORMS-Connect\catalog.json";
            CatalogImportNote.Text = note;
            TrayText.Text = "Connect catalog empty.";
            LogText.Text = note;
            return;
        }
        var n = 0;
        var skipped = 0;
        foreach (var c in catalog)
        {
            if (string.IsNullOrWhiteSpace(c.Id)) continue;
            var existing = _projects.Get(c.Id);
            if (existing is not null)
            {
                skipped++;
                continue;
            }
            _projects.Upsert(
                c.Id,
                string.IsNullOrWhiteSpace(c.Ref) ? c.Id[..Math.Min(8, c.Id.Length)] : c.Ref,
                string.IsNullOrWhiteSpace(c.Title) ? c.Ref : c.Title,
                string.IsNullOrWhiteSpace(c.Status) ? "ACTIVE" : c.Status,
                "",
                "Imported from AORMS Connect",
                "LOCAL");
            n++;
        }
        if (_selectedProjectId is null)
        {
            var first = _projects.List().FirstOrDefault();
            if (first is not null) _selectedProjectId = first.ProjectId;
        }
        ReloadProjects();
        var status =
            n == 0
                ? $"Import complete — 0 new ({skipped} already in firm.db, {catalog.Count} in Connect catalog)."
                : $"Imported {n} from Connect ({skipped} skipped as duplicates). Open selected in Focus when ready.";
        CatalogImportNote.Text = status;
        TrayText.Text = n == 0 ? "No new Connect projects." : $"Imported {n} Connect project(s).";
        LogText.Text = status;
        if (_stage != StageId.Projects)
            ShowStage(StageId.Projects);
        // S2c polish: after first successful import with empty Focus, open the selected project.
        if (n > 0 && _focusProjectId is null && _selectedProjectId is not null)
        {
            _focusProjectId = _selectedProjectId;
            ShowStage(StageId.ProjectFocus);
            TrayText.Text = $"Imported {n} · Focus · {_focusProjectId}";
        }
    }

    void SaveTaskLocal()
    {
        var title = TaskTitleBox.Text?.Trim() ?? "";
        var projectId = ResolveTaskProjectId();
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(projectId))
        {
            TrayText.Text = "Task title and project id required.";
            return;
        }
        var taskId = Guid.NewGuid().ToString("N")[..12];
        _bridge.Db.UpsertLocalTask(taskId, projectId, title, "OPEN", "LOCAL");
        TaskTitleBox.Text = "";
        ReloadTasks();
        TrayText.Text = $"Saved local task {taskId}";
    }

    async Task PublishTaskAsync()
    {
        var title = TaskTitleBox.Text?.Trim() ?? "";
        var projectId = ResolveTaskProjectId();
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(projectId))
        {
            TrayText.Text = "Task title and project id required.";
            return;
        }
        var taskId = Guid.NewGuid().ToString("N")[..12];
        try
        {
            await _bridge.PublishOpsTaskAsync(projectId, taskId, title, "OPEN");
            TaskTitleBox.Text = "";
            ReloadTasks();
            TrayText.Text = $"Published task {taskId}";
        }
        catch (Exception ex)
        {
            TrayText.Text = $"Publish failed: {ex.Message}";
        }
    }

    string ResolveTaskProjectId()
    {
        var typed = TaskProjectBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(typed)) return typed;
        return _focusProjectId ?? _selectedProjectId ?? "";
    }

    void ReloadTasks()
    {
        var rows = _bridge.Db.ListLocalTasks();
        var items = rows.Select(r => new TaskRow
        {
            TaskId = r.TaskId,
            ProjectId = r.ProjectId,
            Title = r.Title,
            Status = r.Status,
            PublishState = r.PublishState,
        }).ToList();
        TasksListView.ItemsSource = items;
        TaskListText.Text = items.Count == 0 ? "(no local tasks)" : $"{items.Count} task(s)";
        if (_selectedTaskId is not null)
        {
            var match = items.FirstOrDefault(i => i.TaskId == _selectedTaskId);
            if (match is not null) TasksListView.SelectedItem = match;
        }
    }

    void ToggleTaskStatus_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTaskId is null)
        {
            TrayText.Text = "Select a task first.";
            return;
        }
        var row = _bridge.Db.ListLocalTasks().FirstOrDefault(t => t.TaskId == _selectedTaskId);
        if (string.IsNullOrEmpty(row.TaskId))
        {
            TrayText.Text = "Task not found.";
            return;
        }
        var next = string.Equals(row.Status, "DONE", StringComparison.OrdinalIgnoreCase) ? "OPEN" : "DONE";
        _bridge.Db.UpsertLocalTask(row.TaskId, row.ProjectId, row.Title, next, row.PublishState);
        ReloadTasks();
        TrayText.Text = $"Task {row.TaskId} → {next}";
    }

    void ClearForm_Click(object sender, RoutedEventArgs e)
    {
        switch (_stage)
        {
            case StageId.Projects:
                ProjectTitleBox.Text = "";
                ProjectRefBox.Text = "";
                ProjectPhaseBox.Text = "";
                break;
            case StageId.ProjectFocus:
                switch (_focusDomain)
                {
                    case FocusDomain.Overview:
                        DecisionTitleBox.Text = "";
                        DecisionKindBox.Text = "";
                        DecisionStatusBox.Text = "";
                        DecisionNotesBox.Text = "";
                        NoteTitleBox.Text = "";
                        NoteKindBox.Text = "";
                        NoteStatusBox.Text = "";
                        NoteNotesBox.Text = "";
                        break;
                    case FocusDomain.Fees:
                        FeeTitleBox.Text = "";
                        FeeAmountBox.Text = "";
                        FeeStatusBox.Text = "";
                        FeeNotesBox.Text = "";
                        break;
                    case FocusDomain.Drawings:
                        DrawingNumberBox.Text = "";
                        DrawingTitleBox.Text = "";
                        DrawingRevBox.Text = "";
                        DrawingStatusBox.Text = "";
                        DrawingPathBox.Text = "";
                        DrawingNotesBox.Text = "";
                        break;
                    case FocusDomain.Documents:
                        DocTitleBox.Text = "";
                        DocKindBox.Text = "";
                        DocStatusBox.Text = "";
                        DocNotesBox.Text = "";
                        break;
                    case FocusDomain.Site:
                        DeliveryKindBox.Text = "";
                        DeliveryTitleBox.Text = "";
                        DeliveryStatusBox.Text = "";
                        DeliveryNotesBox.Text = "";
                        break;
                    default:
                        FocusNotesBox.Text = "";
                        RiskTitleBox.Text = "";
                        RiskKindBox.Text = "";
                        RiskStatusBox.Text = "";
                        RiskNotesBox.Text = "";
                        break;
                }
                break;
            case StageId.Home:
                EstiPromptBox.Text = "";
                EstiReplyText.Text = "";
                break;
            case StageId.Clients:
                ClientNameBox.Text = "";
                ClientContactBox.Text = "";
                ClientEmailBox.Text = "";
                ClientNotesBox.Text = "";
                break;
            case StageId.Tasks:
                TaskTitleBox.Text = "";
                break;
        }
        TrayText.Text = "Form cleared.";
    }

    void DockCreate_Click(object sender, RoutedEventArgs e)
    {
        switch (_stage)
        {
            case StageId.Projects:
                SavePortfolioProject();
                break;
            case StageId.ProjectFocus:
                switch (_focusDomain)
                {
                    case FocusDomain.Overview:
                        SaveOverviewLedger();
                        break;
                    case FocusDomain.Fees:
                        SaveFee();
                        break;
                    case FocusDomain.Drawings:
                        SaveDrawing();
                        break;
                    case FocusDomain.Documents:
                        SaveDocument();
                        break;
                    case FocusDomain.Site:
                        SaveDelivery();
                        break;
                    default:
                        SaveFocusProject();
                        break;
                }
                break;
            case StageId.Clients:
                SaveClient();
                break;
            case StageId.Tasks:
                SaveTaskLocal();
                break;
            case StageId.Home:
                ProbeOllama_Click(sender, e);
                break;
        }
    }

    void DockReload_Click(object sender, RoutedEventArgs e)
    {
        switch (_stage)
        {
            case StageId.Projects:
                ReloadProjects();
                TrayText.Text = "Projects reloaded.";
                break;
            case StageId.ProjectFocus:
                if (_focusDomain == FocusDomain.Brief)
                    LoadFocusForm();
                else
                    ShowFocusDomain(_focusDomain);
                TrayText.Text = "Focus reloaded.";
                break;
            case StageId.Clients:
                ReloadClients();
                TrayText.Text = "Clients reloaded.";
                break;
            case StageId.Home:
                LoadHome();
                RefreshStatus("Status refreshed.");
                break;
            case StageId.Tasks:
                ReloadTasks();
                TrayText.Text = "Tasks reloaded.";
                break;
            case StageId.Stub:
                break;
        }
    }

    async void DockCommit_Click(object sender, RoutedEventArgs e)
    {
        switch (_stage)
        {
            case StageId.Projects:
                await PublishProjectStatusAsync();
                break;
            case StageId.ProjectFocus:
                switch (_focusDomain)
                {
                    case FocusDomain.Overview:
                        await PublishOverviewAsync();
                        break;
                    case FocusDomain.Fees:
                        await PublishFeeAsync();
                        break;
                    case FocusDomain.Drawings:
                        await PublishDrawingAsync();
                        break;
                    case FocusDomain.Documents:
                        await PublishDocumentAsync();
                        break;
                    case FocusDomain.Site:
                        await PublishDeliveryAsync();
                        break;
                    default:
                        await PublishProjectStatusAsync();
                        break;
                }
                break;
            case StageId.Clients:
                await PublishClientAsync();
                break;
            case StageId.Home:
                Flush_Click(sender, e);
                break;
            case StageId.Tasks:
                await PublishTaskAsync();
                break;
        }
    }

    void ReloadClients()
    {
        var rows = _clients.List();
        var items = rows.Select(r => new ClientRow
        {
            ClientId = r.ClientId,
            Name = r.Name,
            Contact = r.Contact,
            Email = r.Email,
            PublishState = r.PublishState,
        }).ToList();
        ClientsListView.ItemsSource = items;
        ClientListText.Text = items.Count == 0 ? "(empty — save a client)" : $"{items.Count} client(s)";
        if (_selectedClientId is null && items.Count > 0)
            _selectedClientId = items[0].ClientId;
        if (_selectedClientId is not null)
        {
            var match = items.FirstOrDefault(i => i.ClientId == _selectedClientId);
            if (match is not null) ClientsListView.SelectedItem = match;
        }
    }

    void SaveClient()
    {
        var name = ClientNameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
        {
            TrayText.Text = "Client name required.";
            return;
        }
        var id = Guid.NewGuid().ToString("N")[..12];
        _clients.Upsert(
            id,
            name,
            ClientContactBox.Text?.Trim() ?? "",
            ClientEmailBox.Text?.Trim() ?? "",
            ClientNotesBox.Text ?? "",
            "LOCAL");
        _selectedClientId = id;
        ClientNameBox.Text = "";
        ClientContactBox.Text = "";
        ClientEmailBox.Text = "";
        ClientNotesBox.Text = "";
        ReloadClients();
        TrayText.Text = $"Saved client {id}";
    }

    async Task PublishClientAsync()
    {
        LocalClient? row = null;
        var name = ClientNameBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(name))
        {
            var id = Guid.NewGuid().ToString("N")[..12];
            _clients.Upsert(id, name, ClientContactBox.Text?.Trim() ?? "",
                ClientEmailBox.Text?.Trim() ?? "", ClientNotesBox.Text ?? "", "LOCAL");
            _selectedClientId = id;
            ClientNameBox.Text = "";
            ClientContactBox.Text = "";
            ClientEmailBox.Text = "";
            ClientNotesBox.Text = "";
            row = _clients.Get(id);
        }
        else if (_selectedClientId is not null)
            row = _clients.Get(_selectedClientId);
        else
            row = _clients.List().FirstOrDefault();

        if (row is null)
        {
            TrayText.Text = "Save a client first.";
            return;
        }
        try
        {
            _bridge.EnqueueMeta("clientStatus", row.ClientId, new Dictionary<string, object?>
            {
                ["clientId"] = row.ClientId,
                ["name"] = row.Name,
                ["contact"] = row.Contact,
                ["email"] = row.Email,
                ["updatedAt"] = DateTime.UtcNow.ToString("O"),
            });
            _clients.Upsert(row.ClientId, row.Name, row.Contact, row.Email, row.Notes, "QUEUED");
            var result = await _bridge.FlushAsync();
            if (result.SkippedReason is not null)
            {
                TrayText.Text = $"Queued clientStatus; flush skipped={result.SkippedReason}";
                LogText.Text = $"Flush skipped={result.SkippedReason}";
            }
            else
            {
                _clients.Upsert(row.ClientId, row.Name, row.Contact, row.Email, row.Notes, "PUBLISHED");
                TrayText.Text = $"Published client · {row.Name} · metaSent={result.MetaSent}";
            }
            ReloadClients();
        }
        catch (Exception ex)
        {
            TrayText.Text = $"Publish failed: {ex.Message}";
        }
    }
}

