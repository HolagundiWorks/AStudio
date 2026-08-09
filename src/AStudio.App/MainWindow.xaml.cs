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

public sealed partial class MainWindow : Window
{
    readonly AormsBridge _bridge;
    readonly LocalProjectsStore _projects;
    ShellModule _module = ShellModule.Portfolio;
    string? _focusProjectId;
    string? _selectedProjectId;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = false;
            _bridge = AormsBridgeHost.CreateFromEnvironment();
            _projects = new LocalProjectsStore(LocalProjectsStore.DefaultFirmDbPath());
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

        DockCreateBtn.Content = module switch
        {
            ShellModule.Portfolio => "Save project",
            ShellModule.Focus => "Save focus",
            ShellModule.Tasks => "Save local",
            _ => "Save local",
        };
        DockCommitBtn.Content = module switch
        {
            ShellModule.Portfolio or ShellModule.Focus => "Publish status",
            ShellModule.Practice => "Flush meta",
            _ => "Publish to hub",
        };
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
            FocusBriefPanel.Visibility = Visibility.Collapsed;
            FocusEmptyCopy.Text =
                "Open Portfolio, pick a local project (or Import from Connect), then Open selected in Focus. Save focus writes the brief; Publish status queues projectStatus meta to the hub.";
            FocusTitleBox.Text = "";
            FocusRefBox.Text = "";
            FocusStatusBox.Text = "";
            FocusPhaseBox.Text = "";
            FocusNotesBox.Text = "";
            FocusMetaText.Text = "";
            UpdateDockEnabled();
            return;
        }

        var p = _projects.Get(id);
        if (p is null)
        {
            FocusSubtitle.Text = $"Missing project {id}";
            FocusEmptyPanel.Visibility = Visibility.Visible;
            FocusBriefPanel.Visibility = Visibility.Collapsed;
            FocusEmptyCopy.Text =
                $"Project {id} is no longer in firm.db. Return to Portfolio and pick another, or Import from Connect.";
            _focusProjectId = null;
            UpdateDockEnabled();
            return;
        }

        _focusProjectId = p.ProjectId;
        FocusEmptyPanel.Visibility = Visibility.Collapsed;
        FocusBriefPanel.Visibility = Visibility.Visible;
        FocusSubtitle.Text = $"{p.ProjectRef} · {p.Title}";
        FocusTitleBox.Text = p.Title;
        FocusRefBox.Text = p.ProjectRef;
        FocusStatusBox.Text = p.Status;
        FocusPhaseBox.Text = p.Phase;
        FocusNotesBox.Text = p.Notes;
        FocusMetaText.Text =
            $"id={p.ProjectId}  publish={p.PublishState}  ·  Save focus (dock) · Publish status (orange)";
        TaskProjectBox.Text = p.ProjectId;
        UpdateDockEnabled();
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
        // From Focus, persist brief edits before enqueue so hub gets current fields.
        if (_module == ShellModule.Focus)
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
                if (FocusBriefPanel.Visibility == Visibility.Visible)
                    FocusNotesBox.Text = "";
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
                SaveFocusProject();
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
                LoadFocusForm();
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
            case ShellModule.Focus:
                await PublishProjectStatusAsync();
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
