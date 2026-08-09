using Aorms.Bridge;
using AStudio.App.Services;
using Microsoft.UI.Xaml;

namespace AStudio.App;

public sealed partial class MainWindow : Window
{
    readonly AormsBridge _bridge;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = false;
        _bridge = AormsBridgeHost.CreateFromEnvironment();
        RefreshStatus("Ready.");
        ReloadTasks_Click(this, new RoutedEventArgs());
    }

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

    void SaveTaskLocal_Click(object sender, RoutedEventArgs e)
    {
        var title = TaskTitleBox.Text?.Trim() ?? "";
        var projectId = TaskProjectBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(projectId))
        {
            RefreshStatus("Task title and project id required.");
            return;
        }
        var taskId = Guid.NewGuid().ToString("N")[..12];
        _bridge.Db.UpsertLocalTask(taskId, projectId, title, "OPEN", "LOCAL");
        TaskTitleBox.Text = "";
        RefreshStatus($"Saved local task {taskId}");
        ReloadTasks_Click(sender, e);
    }

    async void PublishTask_Click(object sender, RoutedEventArgs e)
    {
        var title = TaskTitleBox.Text?.Trim() ?? "";
        var projectId = TaskProjectBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(projectId))
        {
            RefreshStatus("Task title and project id required.");
            return;
        }
        var taskId = Guid.NewGuid().ToString("N")[..12];
        try
        {
            LogText.Text = "Publishing task…";
            await _bridge.PublishOpsTaskAsync(projectId, taskId, title, "OPEN");
            TaskTitleBox.Text = "";
            RefreshStatus($"Published task {taskId} to Mongo ops");
            ReloadTasks_Click(sender, e);
        }
        catch (Exception ex)
        {
            RefreshStatus($"Publish failed: {ex.Message}");
        }
    }

    void ReloadTasks_Click(object sender, RoutedEventArgs e)
    {
        var rows = _bridge.Db.ListLocalTasks();
        TaskListText.Text = rows.Count == 0
            ? "(no local tasks)"
            : string.Join("\n", rows.Select(r => $"{r.TaskId}  {r.Status}/{r.PublishState}  {r.Title}"));
    }

    void ClearForm_Click(object sender, RoutedEventArgs e)
    {
        TaskTitleBox.Text = "";
        RefreshStatus("Form cleared.");
    }
}
