using System.Diagnostics;
using Aorms.Bridge;
using AStudio.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AStudio.App;

enum ShellModule
{
    Focus,
    Portfolio,
    Practice,
    Tasks,
}

enum FocusDomain
{
    Brief,
    Fees,
    Drawings,
    Delivery,
}

public sealed partial class MainWindow : Window
{
    readonly AormsBridge _bridge;
    readonly LocalProjectsStore _projects;
    readonly LocalFeesStore _fees;
    readonly LocalDrawingsStore _drawings;
    readonly LocalDeliveryStore _delivery;
    ShellModule _module = ShellModule.Portfolio;
    FocusDomain _focusDomain = FocusDomain.Brief;
    string? _focusProjectId;
    string? _selectedProjectId;
    string? _selectedFeeId;
    string? _selectedDrawingId;
    string? _selectedDeliveryId;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = false;
            _bridge = AormsBridgeHost.CreateFromEnvironment();
            var dbPath = LocalProjectsStore.DefaultFirmDbPath();
            _projects = new LocalProjectsStore(dbPath);
            _fees = new LocalFeesStore(dbPath);
            _drawings = new LocalDrawingsStore(dbPath);
            _delivery = new LocalDeliveryStore(dbPath);
            ShowModule(ShellModule.Portfolio);
            RefreshStatus("Ready.");
        }
        catch (Exception ex)
        {
            LogStartupFailure(ex);
            throw;
        }
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

    void ShowModule(ShellModule module)
    {
        _module = module;
        PanelFocus.Visibility = module == ShellModule.Focus ? Visibility.Visible : Visibility.Collapsed;
        PanelPortfolio.Visibility = module == ShellModule.Portfolio ? Visibility.Visible : Visibility.Collapsed;
        PanelPractice.Visibility = module == ShellModule.Practice ? Visibility.Visible : Visibility.Collapsed;
        PanelTasks.Visibility = module == ShellModule.Tasks ? Visibility.Visible : Visibility.Collapsed;

        StyleNav(NavFocusBtn, module == ShellModule.Focus);
        StyleNav(NavPortfolioBtn, module == ShellModule.Portfolio);
        StyleNav(NavPracticeBtn, module == ShellModule.Practice);
        StyleNav(NavTasksBtn, module == ShellModule.Tasks);

        // Portfolio dock shows Import (≤5 total: Clear · Import · Save · Reload · Publish).
        DockImportBtn.Visibility = module == ShellModule.Portfolio
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyDockLabels();
        TrayText.Text = $"AStudio · {_module}";

        switch (module)
        {
            case ShellModule.Portfolio:
                ReloadProjects();
                break;
            case ShellModule.Focus:
                LoadFocusForm();
                break;
            case ShellModule.Practice:
                RefreshStatus();
                break;
            case ShellModule.Tasks:
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
        if (_module == ShellModule.Focus)
        {
            DockCreateBtn.Content = _focusDomain switch
            {
                FocusDomain.Fees => "Save fee",
                FocusDomain.Drawings => "Save drawing",
                FocusDomain.Delivery => "Save delivery",
                _ => "Save focus",
            };
            DockCommitBtn.Content = _focusDomain switch
            {
                FocusDomain.Fees => "Publish invoice",
                FocusDomain.Drawings => "Publish register",
                FocusDomain.Delivery => "Publish progress",
                _ => "Publish status",
            };
            return;
        }

        DockCreateBtn.Content = _module switch
        {
            ShellModule.Portfolio => "Save project",
            ShellModule.Tasks => "Save local",
            _ => "Save local",
        };
        DockCommitBtn.Content = _module switch
        {
            ShellModule.Portfolio => "Publish status",
            ShellModule.Practice => "Flush meta",
            _ => "Publish to hub",
        };
    }

    void UpdateDockEnabled()
    {
        var hasFocusProject = ResolveFocusProjectId() is not null;
        switch (_module)
        {
            case ShellModule.Focus:
                DockCreateBtn.IsEnabled = hasFocusProject;
                DockCommitBtn.IsEnabled = hasFocusProject;
                break;
            case ShellModule.Portfolio:
                DockCreateBtn.IsEnabled = true;
                DockCommitBtn.IsEnabled = hasFocusProject || _selectedProjectId is not null
                    || _projects.List().Count > 0;
                break;
            default:
                DockCreateBtn.IsEnabled = true;
                DockCommitBtn.IsEnabled = true;
                break;
        }
    }

    string? ResolveFocusProjectId() => _focusProjectId ?? _selectedProjectId;

    static void StyleNav(Button btn, bool active)
    {
        // Theme styles are not always in Application.Current.Resources (unpackaged WinUI).
        // Use ink/accent paints instead of AccentButtonStyle / SubtleButtonStyle lookups.
        if (active)
        {
            btn.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 0xFF, 0x4F, 0x18));
            btn.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 255, 255, 255));
        }
        else
        {
            btn.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(0, 0, 0, 0));
            btn.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 0x14, 0x15, 0x17));
        }
    }

    void NavFocus_Click(object sender, RoutedEventArgs e) => ShowModule(ShellModule.Focus);
    void NavPortfolio_Click(object sender, RoutedEventArgs e) => ShowModule(ShellModule.Portfolio);
    void NavPractice_Click(object sender, RoutedEventArgs e) => ShowModule(ShellModule.Practice);
    void NavTasks_Click(object sender, RoutedEventArgs e) => ShowModule(ShellModule.Tasks);

    void RefreshStatus(string? note = null)
    {
        var cfg = _bridge.HubConfigured();
        HubStatusText.Text =
            $"hub={cfg.HubUrl}  licenseApi={cfg.LicenseApiUrl}\n" +
            $"hasSyncToken={cfg.HasSyncToken}  syncReady={cfg.SyncReady}";
        if (!string.IsNullOrWhiteSpace(note))
            LogText.Text = note;
    }

    void Refresh_Click(object sender, RoutedEventArgs e) => RefreshStatus("Status refreshed.");

    async void Activate_Click(object sender, RoutedEventArgs e)
    {
        var key = LicenseKeyBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(key))
        {
            RefreshStatus("Enter a licence key first.");
            return;
        }
        try
        {
            LogText.Text = "Activating…";
            var grant = await _bridge.ActivateAsync(key);
            RefreshStatus($"Activate OK · syncToken length={grant.SyncToken?.Length ?? 0}");
        }
        catch (Exception ex)
        {
            RefreshStatus($"Activate failed: {ex.Message}");
        }
    }

    async void Flush_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LogText.Text = "Flushing…";
            var result = await _bridge.FlushAsync();
            if (result.SkippedReason is not null)
                RefreshStatus($"Flush skipped={result.SkippedReason}");
            else
                RefreshStatus($"Flush OK metaSent={result.MetaSent} artSent={result.ArtifactsSent}");
        }
        catch (Exception ex)
        {
            RefreshStatus($"Flush failed: {ex.Message}");
        }
    }

    void ReloadProjects()
    {
        var rows = _projects.List();
        if (rows.Count == 0)
        {
            ProjectListText.Text =
                "(empty — save a project below, or Import from Connect above)";
            UpdateDockEnabled();
            return;
        }
        ProjectListText.Text = string.Join("\n", rows.Select(r =>
        {
            var mark = r.ProjectId == _selectedProjectId || r.ProjectId == _focusProjectId ? ">" : " ";
            return $"{mark} {r.ProjectRef}  {r.Status}/{r.PublishState}  {r.Title}  [{r.ProjectId}]";
        }));
        if (_selectedProjectId is null && rows.Count > 0)
            _selectedProjectId = rows[0].ProjectId;
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
                "Open Portfolio, pick a local project (or Import from Connect), then Open selected in Focus. Brief · Fees · Drawings · Delivery are project-scoped (S3).";
            FocusTitleBox.Text = "";
            FocusRefBox.Text = "";
            FocusStatusBox.Text = "";
            FocusPhaseBox.Text = "";
            FocusNotesBox.Text = "";
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
                $"Project {id} is no longer in firm.db. Return to Portfolio and pick another, or Import from Connect.";
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
        FocusMetaText.Text =
            $"id={p.ProjectId}  publish={p.PublishState}  ·  Brief / Fees / Drawings / Delivery";
        FocusEngineText.Text = BbsEngineClient.DllPresent()
            ? $"bbs_engine.dll ready · {BbsEngineClient.DllPath()}"
            : "bbs_engine.dll missing — run build-engine.cmd then rebuild AStudio.";
        TaskProjectBox.Text = p.ProjectId;
        ShowFocusDomain(_focusDomain);
        UpdateDockEnabled();
    }

    void FocusTabBrief_Click(object sender, RoutedEventArgs e) => ShowFocusDomain(FocusDomain.Brief);
    void FocusTabFees_Click(object sender, RoutedEventArgs e) => ShowFocusDomain(FocusDomain.Fees);
    void FocusTabDrawings_Click(object sender, RoutedEventArgs e) => ShowFocusDomain(FocusDomain.Drawings);
    void FocusTabDelivery_Click(object sender, RoutedEventArgs e) => ShowFocusDomain(FocusDomain.Delivery);

    void ShowFocusDomain(FocusDomain domain)
    {
        _focusDomain = domain;
        FocusBriefPanel.Visibility = domain == FocusDomain.Brief ? Visibility.Visible : Visibility.Collapsed;
        FocusFeesPanel.Visibility = domain == FocusDomain.Fees ? Visibility.Visible : Visibility.Collapsed;
        FocusDrawingsPanel.Visibility = domain == FocusDomain.Drawings ? Visibility.Visible : Visibility.Collapsed;
        FocusDeliveryPanel.Visibility = domain == FocusDomain.Delivery ? Visibility.Visible : Visibility.Collapsed;

        StyleNav(FocusTabBriefBtn, domain == FocusDomain.Brief);
        StyleNav(FocusTabFeesBtn, domain == FocusDomain.Fees);
        StyleNav(FocusTabDrawingsBtn, domain == FocusDomain.Drawings);
        StyleNav(FocusTabDeliveryBtn, domain == FocusDomain.Delivery);

        switch (domain)
        {
            case FocusDomain.Fees:
                ReloadFees();
                break;
            case FocusDomain.Drawings:
                ReloadDrawings();
                break;
            case FocusDomain.Delivery:
                ReloadDelivery();
                break;
        }

        ApplyDockLabels();
        UpdateDockEnabled();
    }

    void ReloadFees()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            FeeListText.Text = "(no project)";
            return;
        }
        var rows = _fees.ListByProject(projectId);
        if (rows.Count == 0)
        {
            FeeListText.Text = "(no fees yet — fill title + amount, Save fee)";
            _selectedFeeId = null;
            return;
        }
        FeeListText.Text = string.Join("\n", rows.Select(r =>
        {
            var mark = r.FeeId == _selectedFeeId ? ">" : " ";
            return $"{mark} {MoneyPaise.FormatInr(r.AmountPaise)}  {r.Status}/{r.PublishState}  {r.Title}  [{r.FeeId}]";
        }));
        _selectedFeeId ??= rows[0].FeeId;
    }

    void ReloadDrawings()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            DrawingListText.Text = "(no project)";
            return;
        }
        var rows = _drawings.ListByProject(projectId);
        if (rows.Count == 0)
        {
            DrawingListText.Text = "(no drawings yet — fill number + title, Save drawing)";
            _selectedDrawingId = null;
            return;
        }
        DrawingListText.Text = string.Join("\n", rows.Select(r =>
        {
            var mark = r.DrawingId == _selectedDrawingId ? ">" : " ";
            return $"{mark} {r.Number}-Rev{r.Rev}  {r.Status}/{r.PublishState}  {r.Title}  [{r.DrawingId}]";
        }));
        _selectedDrawingId ??= rows[0].DrawingId;
    }

    void ReloadDelivery()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            DeliveryListText.Text = "(no project)";
            return;
        }
        var rows = _delivery.ListByProject(projectId);
        if (rows.Count == 0)
        {
            DeliveryListText.Text = "(no delivery items — fill kind + title, Save delivery)";
            _selectedDeliveryId = null;
            return;
        }
        DeliveryListText.Text = string.Join("\n", rows.Select(r =>
        {
            var mark = r.ItemId == _selectedDeliveryId ? ">" : " ";
            return $"{mark} {r.Kind}  {r.Status}/{r.PublishState}  {r.Title}  [{r.ItemId}]";
        }));
        _selectedDeliveryId ??= rows[0].ItemId;
    }

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
            existing.PublishState);
        LoadFocusForm();
        if (!quiet) TrayText.Text = $"Saved focus · {projectRef} ({existing.PublishState})";
        return true;
    }

    async Task PublishProjectStatusAsync()
    {
        // From Focus brief, persist edits before enqueue so hub gets current fields.
        if (_module == ShellModule.Focus && _focusDomain == FocusDomain.Brief)
        {
            if (!SaveFocusProject(quiet: true))
            {
                TrayText.Text = "Save focus first — no project or title/ref missing.";
                return;
            }
        }

        var id = ResolveFocusProjectId();
        if (id is null)
        {
            TrayText.Text = "No project to publish — select one in Portfolio.";
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
            _bridge.EnqueueMeta("projectStatus", p.ProjectId, new Dictionary<string, object?>
            {
                ["projectId"] = p.ProjectId,
                ["ref"] = p.ProjectRef,
                ["title"] = p.Title,
                ["status"] = p.Status,
                ["phase"] = p.Phase,
                ["updatedAt"] = DateTime.UtcNow.ToString("O"),
            });
            _projects.Upsert(p.ProjectId, p.ProjectRef, p.Title, p.Status, p.Phase, p.Notes, "QUEUED");
            var result = await _bridge.FlushAsync();
            if (result.SkippedReason is not null)
            {
                TrayText.Text =
                    $"Queued projectStatus for {p.ProjectRef}; flush skipped={result.SkippedReason} — activate on Practice if needed.";
                LogText.Text = $"Flush skipped={result.SkippedReason}";
            }
            else
            {
                _projects.Upsert(p.ProjectId, p.ProjectRef, p.Title, p.Status, p.Phase, p.Notes, "PUBLISHED");
                TrayText.Text = $"Published status · {p.ProjectRef} · metaSent={result.MetaSent}";
                LogText.Text = $"projectStatus OK · {p.ProjectId}";
            }
            // Stay on current module (do not yank to Practice).
            if (_module == ShellModule.Portfolio) ReloadProjects();
            if (_module == ShellModule.Focus) LoadFocusForm();
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
                    $"Queued invoiceStatus; flush skipped={result.SkippedReason} — Activate on Practice.";
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
        _drawings.Upsert(id, projectId, number, title, rev, status, DrawingNotesBox.Text ?? "", "LOCAL");
        _selectedDrawingId = id;
        DrawingNumberBox.Text = "";
        DrawingTitleBox.Text = "";
        DrawingRevBox.Text = "";
        DrawingStatusBox.Text = "";
        DrawingNotesBox.Text = "";
        ReloadDrawings();
        TrayText.Text = $"Saved drawing {number}-Rev{rev}";
    }

    async Task PublishDrawingAsync()
    {
        var projectId = ResolveFocusProjectId();
        if (projectId is null)
        {
            TrayText.Text = "No project in focus.";
            return;
        }

        LocalDrawing? row = null;
        var number = DrawingNumberBox.Text?.Trim() ?? "";
        var title = DrawingTitleBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(number) && !string.IsNullOrEmpty(title))
        {
            var id = Guid.NewGuid().ToString("N")[..12];
            var rev = string.IsNullOrWhiteSpace(DrawingRevBox.Text) ? "A" : DrawingRevBox.Text.Trim();
            var status = string.IsNullOrWhiteSpace(DrawingStatusBox.Text)
                ? "READY"
                : DrawingStatusBox.Text.Trim().ToUpperInvariant();
            _drawings.Upsert(id, projectId, number, title, rev, status, DrawingNotesBox.Text ?? "", "LOCAL");
            _selectedDrawingId = id;
            DrawingNumberBox.Text = "";
            DrawingTitleBox.Text = "";
            DrawingRevBox.Text = "";
            DrawingStatusBox.Text = "";
            DrawingNotesBox.Text = "";
            row = _drawings.Get(id);
        }
        else if (_selectedDrawingId is not null)
        {
            row = _drawings.Get(_selectedDrawingId);
        }
        else
        {
            row = _drawings.ListByProject(projectId).FirstOrDefault();
        }

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
                ["updatedAt"] = DateTime.UtcNow.ToString("O"),
            });
            _drawings.Upsert(row.DrawingId, row.ProjectId, row.Number, row.Title, row.Rev, row.Status, row.Notes, "QUEUED");
            var result = await _bridge.FlushAsync();
            if (result.SkippedReason is not null)
            {
                TrayText.Text =
                    $"Queued drawingRegister; flush skipped={result.SkippedReason} — Activate on Practice.";
                LogText.Text = $"Flush skipped={result.SkippedReason}";
            }
            else
            {
                _drawings.Upsert(row.DrawingId, row.ProjectId, row.Number, row.Title, row.Rev, row.Status, row.Notes, "PUBLISHED");
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
                    $"Queued phaseProgress; flush skipped={result.SkippedReason} — Activate on Practice.";
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
        ShowModule(ShellModule.Focus);
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
        // Dev smoke: sibling BBSApp / Estimation product shells under vendor pin.
        var repoGuess = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "vendor", "AQC", "BBSDesktop"));
        candidates.Add(Path.Combine(repoGuess, "AQC.Estimation", "bin", "Release", "net8.0", "AQC.Estimation.exe"));
        candidates.Add(Path.Combine(repoGuess, "BBSApp", "bin", "x64", "Release", "net8.0-windows10.0.19041.0", "BBSApp.exe"));

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
        if (_module != ShellModule.Portfolio)
            ShowModule(ShellModule.Portfolio);
        // S2c polish: after first successful import with empty Focus, open the selected project.
        if (n > 0 && _focusProjectId is null && _selectedProjectId is not null)
        {
            _focusProjectId = _selectedProjectId;
            ShowModule(ShellModule.Focus);
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
        TaskListText.Text = rows.Count == 0
            ? "(no local tasks)"
            : string.Join("\n", rows.Select(r => $"{r.TaskId}  {r.Status}/{r.PublishState}  {r.Title}"));
    }

    void ClearForm_Click(object sender, RoutedEventArgs e)
    {
        switch (_module)
        {
            case ShellModule.Portfolio:
                ProjectTitleBox.Text = "";
                ProjectRefBox.Text = "";
                ProjectPhaseBox.Text = "";
                break;
            case ShellModule.Focus:
                switch (_focusDomain)
                {
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
                        DrawingNotesBox.Text = "";
                        break;
                    case FocusDomain.Delivery:
                        DeliveryKindBox.Text = "";
                        DeliveryTitleBox.Text = "";
                        DeliveryStatusBox.Text = "";
                        DeliveryNotesBox.Text = "";
                        break;
                    default:
                        FocusNotesBox.Text = "";
                        break;
                }
                break;
            case ShellModule.Tasks:
                TaskTitleBox.Text = "";
                break;
        }
        TrayText.Text = "Form cleared.";
    }

    void DockCreate_Click(object sender, RoutedEventArgs e)
    {
        switch (_module)
        {
            case ShellModule.Portfolio:
                SavePortfolioProject();
                break;
            case ShellModule.Focus:
                switch (_focusDomain)
                {
                    case FocusDomain.Fees:
                        SaveFee();
                        break;
                    case FocusDomain.Drawings:
                        SaveDrawing();
                        break;
                    case FocusDomain.Delivery:
                        SaveDelivery();
                        break;
                    default:
                        SaveFocusProject();
                        break;
                }
                break;
            case ShellModule.Tasks:
                SaveTaskLocal();
                break;
            case ShellModule.Practice:
                RefreshStatus("Nothing to save on Practice — use Activate.");
                break;
        }
    }

    void DockReload_Click(object sender, RoutedEventArgs e)
    {
        switch (_module)
        {
            case ShellModule.Portfolio:
                ReloadProjects();
                TrayText.Text = "Projects reloaded.";
                break;
            case ShellModule.Focus:
                if (_focusDomain == FocusDomain.Brief)
                    LoadFocusForm();
                else
                    ShowFocusDomain(_focusDomain);
                TrayText.Text = "Focus reloaded.";
                break;
            case ShellModule.Practice:
                RefreshStatus("Status refreshed.");
                break;
            case ShellModule.Tasks:
                ReloadTasks();
                TrayText.Text = "Tasks reloaded.";
                break;
        }
    }

    async void DockCommit_Click(object sender, RoutedEventArgs e)
    {
        switch (_module)
        {
            case ShellModule.Portfolio:
                await PublishProjectStatusAsync();
                break;
            case ShellModule.Focus:
                switch (_focusDomain)
                {
                    case FocusDomain.Fees:
                        await PublishFeeAsync();
                        break;
                    case FocusDomain.Drawings:
                        await PublishDrawingAsync();
                        break;
                    case FocusDomain.Delivery:
                        await PublishDeliveryAsync();
                        break;
                    default:
                        await PublishProjectStatusAsync();
                        break;
                }
                break;
            case ShellModule.Practice:
                Flush_Click(sender, e);
                break;
            case ShellModule.Tasks:
                await PublishTaskAsync();
                break;
        }
    }
}
