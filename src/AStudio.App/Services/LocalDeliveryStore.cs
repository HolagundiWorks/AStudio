// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using Microsoft.Data.Sqlite;

namespace AStudio.App.Services;

/// <summary>
/// Site delivery items in firm.db (S3) — snags · instructions · progress.
/// Publish phaseProgress meta (allow-listed). BBS calc stays in AQC / engine smoke.
/// </summary>
public sealed class LocalDeliveryStore : IDisposable
{
    readonly SqliteConnection _con;

    public LocalDeliveryStore(string firmDbPath)
    {
        var dir = Path.GetDirectoryName(firmDbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        SQLitePCL.Batteries_V2.Init();
        _con = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = firmDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        _con.Open();
        EnsureSchema();
    }

    void EnsureSchema()
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS local_delivery(
              item_id TEXT PRIMARY KEY,
              project_id TEXT NOT NULL,
              kind TEXT NOT NULL DEFAULT 'PROGRESS',
              title TEXT NOT NULL,
              status TEXT NOT NULL DEFAULT 'OPEN',
              notes TEXT NOT NULL DEFAULT '',
              publish_state TEXT NOT NULL DEFAULT 'LOCAL',
              updated_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_local_delivery_project ON local_delivery(project_id);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Upsert(
        string itemId,
        string projectId,
        string kind,
        string title,
        string status,
        string notes,
        string publishState)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            INSERT INTO local_delivery(
              item_id, project_id, kind, title, status, notes, publish_state, updated_at)
            VALUES($id,$p,$k,$t,$s,$n,$ps,$u)
            ON CONFLICT(item_id) DO UPDATE SET
              project_id=excluded.project_id,
              kind=excluded.kind,
              title=excluded.title,
              status=excluded.status,
              notes=excluded.notes,
              publish_state=excluded.publish_state,
              updated_at=excluded.updated_at
            """;
        cmd.Parameters.AddWithValue("$id", itemId);
        cmd.Parameters.AddWithValue("$p", projectId);
        cmd.Parameters.AddWithValue("$k", kind);
        cmd.Parameters.AddWithValue("$t", title);
        cmd.Parameters.AddWithValue("$s", status);
        cmd.Parameters.AddWithValue("$n", notes);
        cmd.Parameters.AddWithValue("$ps", publishState);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public LocalDeliveryItem? Get(string itemId)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            SELECT item_id, project_id, kind, title, status, notes, publish_state
            FROM local_delivery WHERE item_id=$id
            """;
        cmd.Parameters.AddWithValue("$id", itemId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return Read(r);
    }

    public IReadOnlyList<LocalDeliveryItem> ListByProject(string projectId)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            SELECT item_id, project_id, kind, title, status, notes, publish_state
            FROM local_delivery WHERE project_id=$p ORDER BY updated_at DESC LIMIT 100
            """;
        cmd.Parameters.AddWithValue("$p", projectId);
        using var r = cmd.ExecuteReader();
        var list = new List<LocalDeliveryItem>();
        while (r.Read()) list.Add(Read(r));
        return list;
    }

    static LocalDeliveryItem Read(SqliteDataReader r) => new(
        r.GetString(0),
        r.GetString(1),
        r.GetString(2),
        r.GetString(3),
        r.GetString(4),
        r.GetString(5),
        r.GetString(6));

    public void Dispose() => _con.Dispose();
}

public sealed record LocalDeliveryItem(
    string ItemId,
    string ProjectId,
    string Kind,
    string Title,
    string Status,
    string Notes,
    string PublishState);
