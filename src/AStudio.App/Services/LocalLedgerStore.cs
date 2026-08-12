// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using Microsoft.Data.Sqlite;

namespace AStudio.App.Services;

/// <summary>Generic project-scoped ledger rows (decisions · notes · documents · risks).</summary>
public sealed class LocalLedgerStore : IDisposable
{
    readonly SqliteConnection _con;
    readonly string _table;

    public LocalLedgerStore(SqliteConnection sharedConnection, string tableName)
    {
        _con = sharedConnection;
        _table = tableName;
        EnsureSchema();
    }

    void EnsureSchema()
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {_table}(
              item_id TEXT PRIMARY KEY,
              project_id TEXT NOT NULL,
              title TEXT NOT NULL,
              kind TEXT NOT NULL DEFAULT '',
              status TEXT NOT NULL DEFAULT 'OPEN',
              notes TEXT NOT NULL DEFAULT '',
              publish_state TEXT NOT NULL DEFAULT 'LOCAL',
              updated_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_{_table}_project ON {_table}(project_id);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Upsert(
        string itemId,
        string projectId,
        string title,
        string kind,
        string status,
        string notes,
        string publishState)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO {_table}(item_id, project_id, title, kind, status, notes, publish_state, updated_at)
            VALUES($id,$p,$t,$k,$s,$n,$ps,$u)
            ON CONFLICT(item_id) DO UPDATE SET
              title=excluded.title,
              kind=excluded.kind,
              status=excluded.status,
              notes=excluded.notes,
              publish_state=excluded.publish_state,
              updated_at=excluded.updated_at
            """;
        cmd.Parameters.AddWithValue("$id", itemId);
        cmd.Parameters.AddWithValue("$p", projectId);
        cmd.Parameters.AddWithValue("$t", title);
        cmd.Parameters.AddWithValue("$k", kind ?? "");
        cmd.Parameters.AddWithValue("$s", status);
        cmd.Parameters.AddWithValue("$n", notes ?? "");
        cmd.Parameters.AddWithValue("$ps", publishState);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public LocalLedgerItem? Get(string itemId)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = $"""
            SELECT item_id, project_id, title, kind, status, notes, publish_state
            FROM {_table} WHERE item_id=$id
            """;
        cmd.Parameters.AddWithValue("$id", itemId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return Read(r);
    }

    public IReadOnlyList<LocalLedgerItem> ListByProject(string projectId, string? kindFilter = null)
    {
        using var cmd = _con.CreateCommand();
        if (string.IsNullOrEmpty(kindFilter))
        {
            cmd.CommandText = $"""
                SELECT item_id, project_id, title, kind, status, notes, publish_state
                FROM {_table} WHERE project_id=$p ORDER BY updated_at DESC LIMIT 200
                """;
        }
        else
        {
            cmd.CommandText = $"""
                SELECT item_id, project_id, title, kind, status, notes, publish_state
                FROM {_table} WHERE project_id=$p AND kind=$k ORDER BY updated_at DESC LIMIT 200
                """;
            cmd.Parameters.AddWithValue("$k", kindFilter);
        }
        cmd.Parameters.AddWithValue("$p", projectId);
        using var r = cmd.ExecuteReader();
        var list = new List<LocalLedgerItem>();
        while (r.Read()) list.Add(Read(r));
        return list;
    }

    static LocalLedgerItem Read(SqliteDataReader r) => new(
        r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3),
        r.GetString(4), r.GetString(5), r.GetString(6));

    public void Dispose() { /* connection owned by LocalProjectsStore */ }
}

public sealed record LocalLedgerItem(
    string ItemId,
    string ProjectId,
    string Title,
    string Kind,
    string Status,
    string Notes,
    string PublishState);
