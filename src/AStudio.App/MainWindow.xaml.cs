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

    void Enqueue_Click(object sender, RoutedEventArgs e)
    {
        var id = _bridge.EnqueueMeta("phaseProgress", "astudio-shell-1", new Dictionary<string, object?>
        {
            ["projectId"] = "00000000-0000-0000-0000-000000000001",
            ["phaseId"] = "astudio-shell-1",
            ["pctComplete"] = 10,
            ["status"] = "IN_PROGRESS",
            ["source"] = "AStudio.App",
        });
        RefreshStatus($"Enqueued meta id={id}");
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
}
