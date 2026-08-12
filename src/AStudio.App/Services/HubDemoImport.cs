// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Text.Json;
using Aorms.Bridge;

namespace AStudio.App.Services;

/// <summary>
/// Pull hub demo projectoffice rows (exported JSON) into Connect catalog + firm.db.
/// Hub Postgres does not auto-sync down — this is the local-dev bridge for demo data.
/// </summary>
public static class HubDemoImport
{
    public static string ExportPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AStudio",
            "hub-demo-projects.json");

    public sealed class HubDemoProject
    {
        public string Id { get; set; } = "";
        public string Ref { get; set; } = "";
        public string Title { get; set; } = "";
        public string Status { get; set; } = "ACTIVE";
        public string? UpdatedAt { get; set; }
    }

    public sealed class ImportResult
    {
        public int CatalogCount { get; init; }
        public int Imported { get; init; }
        public int Skipped { get; init; }
        public string Note { get; init; } = "";
    }

    public static IReadOnlyList<HubDemoProject> LoadExport(string? path = null)
    {
        path ??= ExportPath;
        if (!File.Exists(path)) return Array.Empty<HubDemoProject>();
        try
        {
            var rows = JsonSerializer.Deserialize<List<HubDemoProject>>(File.ReadAllText(path));
            return rows ?? new List<HubDemoProject>();
        }
        catch
        {
            return Array.Empty<HubDemoProject>();
        }
    }

    public static void WriteConnectCatalog(IReadOnlyList<HubDemoProject> rows)
    {
        var dir = ConnectSession.DefaultDirectory();
        Directory.CreateDirectory(dir);
        var catalog = rows.Select(r => new CatalogProject
        {
            Id = r.Id,
            Ref = r.Ref,
            Title = r.Title,
            Status = string.IsNullOrWhiteSpace(r.Status) ? "ACTIVE" : r.Status,
            UpdatedAt = r.UpdatedAt ?? DateTime.UtcNow.ToString("O"),
        }).ToList();
        var path = ConnectCatalog.DefaultPath();
        File.WriteAllText(path, JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static ImportResult ImportIntoFirm(LocalProjectsStore projects, IReadOnlyList<HubDemoProject>? rows = null)
    {
        rows ??= LoadExport();
        if (rows.Count == 0)
        {
            rows = ConnectCatalog.List().Select(c => new HubDemoProject
            {
                Id = c.Id,
                Ref = c.Ref,
                Title = c.Title,
                Status = c.Status,
                UpdatedAt = c.UpdatedAt,
            }).ToList();
        }
        if (rows.Count == 0)
        {
            return new ImportResult
            {
                Note = $"No export at {ExportPath}. Run sync-demo-from-hub.cmd first.",
            };
        }

        WriteConnectCatalog(rows);
        var imported = 0;
        var skipped = 0;
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.Id) || string.IsNullOrWhiteSpace(r.Title))
            {
                skipped++;
                continue;
            }
            if (projects.Get(r.Id) is not null)
            {
                skipped++;
                continue;
            }
            projects.Upsert(
                r.Id,
                string.IsNullOrWhiteSpace(r.Ref) ? r.Id[..8] : r.Ref,
                r.Title,
                string.IsNullOrWhiteSpace(r.Status) ? "ACTIVE" : r.Status,
                phase: "",
                notes: "Imported from hub demo (esti_projectoffice)",
                publishState: "LOCAL");
            imported++;
        }

        return new ImportResult
        {
            CatalogCount = rows.Count,
            Imported = imported,
            Skipped = skipped,
            Note = imported == 0
                ? $"Catalog {rows.Count} · 0 new ({skipped} already local)."
                : $"Imported {imported} hub demo projects ({skipped} skipped).",
        };
    }

    /// <summary>Enqueue projectStatus for every local project and Flush.</summary>
    public static async Task<(int Queued, FlushResult Flush)> PublishAllAsync(
        AormsBridge bridge,
        LocalProjectsStore projects,
        CancellationToken ct = default)
    {
        var queued = 0;
        foreach (var p in projects.List())
        {
            bridge.EnqueueMeta("projectStatus", p.ProjectId, new Dictionary<string, object?>
            {
                ["projectId"] = p.ProjectId,
                ["projectRef"] = p.ProjectRef,
                ["title"] = p.Title,
                ["status"] = p.Status,
                ["phase"] = p.Phase,
                ["publishState"] = "PUBLISHED",
            });
            projects.SetPublishState(p.ProjectId, "QUEUED");
            queued++;
        }
        var flush = await bridge.FlushAsync(ct).ConfigureAwait(false);
        if (flush.SkippedReason is null)
        {
            foreach (var p in projects.List())
                projects.SetPublishState(p.ProjectId, "PUBLISHED");
        }
        return (queued, flush);
    }
}
