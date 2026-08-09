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
    }

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
            ProjectListText.Text = "(no local projects — save one or import Connect catalog)";
            return;
        }
        ProjectListText.Text = string.Join("\n", rows.Select(r =>
        {
            var mark = r.ProjectId == _selectedProjectId || r.ProjectId == _focusProjectId ? ">" : " ";
            return $"{mark} {r.ProjectRef}  {r.Status}/{r.PublishState}  {r.Title}  [{r.ProjectId}]";
        }));
        if (_selectedProjectId is null && rows.Count > 0)
            _selectedProjectId = rows[0].ProjectId;
    }

    void LoadFocusForm()
    {
        var id = _focusProjectId ?? _selectedProjectId;
        if (id is null)
        {
            FocusSubtitle.Text = "No project selected — open one from Portfolio.";
            FocusTitleBox.Text = "";
            FocusRefBox.Text = "";
            FocusStatusBox.Text = "";
            FocusPhaseBox.Text = "";
            FocusNotesBox.Text = "";
            FocusMetaText.Text = "";
            return;
        }
        var p = _projects.Get(id);
        if (p is null)
        {
            FocusSubtitle.Text = $"Missing project {id}";
            return;
        }
        _focusProjectId = p.ProjectId;
        FocusSubtitle.Text = $"{p.ProjectRef} · {p.PublishState}";
        FocusTitleBox.Text = p.Title;
        FocusRefBox.Text = p.ProjectRef;
        FocusStatusBox.Text = p.Status;
        FocusPhaseBox.Text = p.Phase;
        FocusNotesBox.Text = p.Notes;
        FocusMetaText.Text = $"id={p.ProjectId}";
        TaskProjectBox.Text = p.ProjectId;
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

    void SaveFocusProject()
    {
        var id = _focusProjectId ?? _selectedProjectId;
        if (id is null)
        {
            TrayText.Text = "Select a project in Portfolio first.";
            return;
        }
        var existing = _projects.Get(id);
        if (existing is null)
        {
            TrayText.Text = "Project not found.";
            return;
        }
        var title = FocusTitleBox.Text?.Trim() ?? "";
        var projectRef = FocusRefBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(projectRef))
        {
            TrayText.Text = "Title and ref required.";
            return;
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
        TrayText.Text = $"Saved focus {id}";
    }

    async Task PublishProjectStatusAsync()
    {
        var id = _focusProjectId ?? _selectedProjectId;
        if (id is null)
        {
            TrayText.Text = "No project to publish.";
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
                TrayText.Text = $"Queued projectStatus; flush skipped={result.SkippedReason}";
                ShowModule(ShellModule.Practice);
                RefreshStatus($"Flush skipped={result.SkippedReason}");
            }
            else
            {
                _projects.Upsert(p.ProjectId, p.ProjectRef, p.Title, p.Status, p.Phase, p.Notes, "PUBLISHED");
                TrayText.Text = $"Published projectStatus · metaSent={result.MetaSent}";
            }
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
            TrayText.Text = "No projects yet.";
            return;
        }
        _focusProjectId = _selectedProjectId ?? rows[0].ProjectId;
        _selectedProjectId = _focusProjectId;
        ShowModule(ShellModule.Focus);
    }

    void ImportCatalog_Click(object sender, RoutedEventArgs e)
    {
        var catalog = ConnectCatalog.List();
        if (catalog.Count == 0)
        {
            TrayText.Text = "Connect catalog empty — %LocalAppData%\\AORMS-Connect\\catalog.json";
            return;
        }
        var n = 0;
        foreach (var c in catalog)
        {
            if (string.IsNullOrWhiteSpace(c.Id)) continue;
            var existing = _projects.Get(c.Id);
            if (existing is not null) continue;
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
        ReloadProjects();
        TrayText.Text = $"Imported {n} Connect project(s).";
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
