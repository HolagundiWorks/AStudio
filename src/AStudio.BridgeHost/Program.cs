using Aorms.Bridge;

var opt = new BridgeOptions
{
    DeviceId = Environment.GetEnvironmentVariable("INSTALL_ID") ?? "astudio-dev-1",
    HubUrl = Environment.GetEnvironmentVariable("ESTI_HUB_URL") ?? "http://127.0.0.1:4000",
    LicenseApiUrl = Environment.GetEnvironmentVariable("ESTI_LICENSE_API_URL") ?? "",
    ProductApiKey = Environment.GetEnvironmentVariable("ESTI_PRODUCT_API_KEY") ?? "",
};
using var bridge = new AormsBridge(opt);
var cfg = bridge.HubConfigured();
Console.WriteLine($"AStudio BridgeHost syncReady={cfg.SyncReady} hasToken={cfg.HasSyncToken} hub={cfg.HubUrl}");
Console.WriteLine("OK ProjectReference to Aorms.Bridge (D5 consume smoke).");
