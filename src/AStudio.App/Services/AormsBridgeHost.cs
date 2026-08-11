// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using Aorms.Bridge;

namespace AStudio.App.Services;

/// <summary>
/// Factory for the AORMS hub bridge (firm.db under LocalAppData\AStudio).
/// Imports AORMS Connect session.json when present (C2 SSO).
/// Hub bind settings: env → local-hub.json → loopback defaults.
/// </summary>
public static class AormsBridgeHost
{
    public static LocalHubConfig LastConfig { get; private set; } = new();

    public static AormsBridge CreateFromEnvironment()
    {
        var cfg = LocalHubConfig.Resolve();
        LastConfig = cfg;
        var opt = new BridgeOptions
        {
            LicenseApiUrl = cfg.LicenseApiUrl,
            HubUrl = cfg.HubUrl,
            ProductApiKey = cfg.ProductApiKey,
            DeviceId = cfg.InstallId,
            DeviceName = "AStudio",
        };
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AStudio",
            "firm.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var bridge = new AormsBridge(opt, dbPath);
        bridge.TryImportConnectSession(overwrite: true);
        return bridge;
    }
}
