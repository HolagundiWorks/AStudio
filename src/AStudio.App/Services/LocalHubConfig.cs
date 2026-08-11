// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Text.Json;

namespace AStudio.App.Services;

/// <summary>
/// Persisted local-Docker hub bind settings under %LocalAppData%\AStudio\local-hub.json.
/// Survives launches that don't inherit run-local-hub.cmd env (Explorer / IDE).
/// </summary>
public sealed class LocalHubConfig
{
    public string HubUrl { get; set; } = "http://127.0.0.1:4000";
    public string LicenseApiUrl { get; set; } = "http://127.0.0.1:4000/platform";
    public string ProductApiKey { get; set; } = "";
    public string InstallId { get; set; } = "";

    public static string ConfigPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AStudio",
            "local-hub.json");

    /// <summary>Fixed key from seed:desktop-local-hub — local Docker only.</summary>
    public const string LocalDevProductApiKey = "hlp_sk_local_desktop_dev_do_not_use_in_prod";

    public static LocalHubConfig LoadOrDefault()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<LocalHubConfig>(json);
                if (cfg is not null) return cfg;
            }
        }
        catch { /* fall through */ }

        return new LocalHubConfig();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>Merge env vars (win) over file, then loopback defaults.</summary>
    public static LocalHubConfig Resolve()
    {
        var file = LoadOrDefault();
        var hub = Env("ESTI_HUB_URL") ?? file.HubUrl;
        if (string.IsNullOrWhiteSpace(hub)) hub = "http://127.0.0.1:4000";
        hub = hub.TrimEnd('/');

        var licenseApi = Env("ESTI_LICENSE_API_URL") ?? file.LicenseApiUrl;
        if (string.IsNullOrWhiteSpace(licenseApi))
            licenseApi = $"{hub}/platform";

        var productKey = Env("ESTI_PRODUCT_API_KEY") ?? file.ProductApiKey;
        if (string.IsNullOrWhiteSpace(productKey) && IsLoopback(hub))
            productKey = LocalDevProductApiKey;

        // Licence key / Activate: AORMS Connect only (session.json SSO).
        var installId = Env("INSTALL_ID") ?? file.InstallId;
        if (string.IsNullOrWhiteSpace(installId))
            installId = $"astudio-{Environment.MachineName}".ToLowerInvariant();

        var resolved = new LocalHubConfig
        {
            HubUrl = hub,
            LicenseApiUrl = licenseApi.TrimEnd('/'),
            ProductApiKey = productKey ?? "",
            InstallId = installId,
        };

        // Persist so next Explorer launch still binds to local hub.
        if (IsLoopback(resolved.HubUrl) && !string.IsNullOrWhiteSpace(resolved.ProductApiKey))
        {
            try { resolved.Save(); } catch { /* best-effort */ }
        }

        return resolved;
    }

    static string? Env(string name)
    {
        var v = Environment.GetEnvironmentVariable(name)?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    static bool IsLoopback(string hubUrl)
    {
        if (!Uri.TryCreate(hubUrl, UriKind.Absolute, out var u)) return false;
        return u.Host is "127.0.0.1" or "localhost" or "::1";
    }
}
