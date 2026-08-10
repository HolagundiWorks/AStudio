// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using Microsoft.Data.Sqlite;

namespace AStudio.App.Services;

/// <summary>Drawing register rows in firm.db (S3). READY → publish drawingRegister meta.</summary>
public sealed class LocalDrawingsStore : IDisposable
{
    readonly SqliteConnection _con;

    public LocalDrawingsStore(string firmDbPath)
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
            CREATE TABLE IF NOT EXISTS local_drawings(
              drawing_id TEXT PRIMARY KEY,
              project_id TEXT NOT NULL,
              number TEXT NOT NULL,
              title TEXT NOT NULL,
              rev TEXT NOT NULL DEFAULT 'A',
              status TEXT NOT NULL DEFAULT 'WIP',
              notes TEXT NOT NULL DEFAULT '',
              publish_state TEXT NOT NULL DEFAULT 'LOCAL',
              updated_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_local_drawings_project ON local_drawings(project_id);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Upsert(
        string drawingId,
        string projectId,
        string number,
        string title,
        string rev,
        string status,
        string notes,
        string publishState)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            INSERT INTO local_drawings(
              drawing_id, project_id, number, title, rev, status, notes, publish_state, updated_at)
            VALUES($id,$p,$num,$t,$rev,$s,$n,$ps,$u)
            ON CONFLICT(drawing_id) DO UPDATE SET
              project_id=excluded.project_id,
              number=excluded.number,
              title=excluded.title,
              rev=excluded.rev,
              status=excluded.status,
              notes=excluded.notes,
              publish_state=excluded.publish_state,
              updated_at=excluded.updated_at
            """;
        cmd.Parameters.AddWithValue("$id", drawingId);
        cmd.Parameters.AddWithValue("$p", projectId);
        cmd.Parameters.AddWithValue("$num", number);
        cmd.Parameters.AddWithValue("$t", title);
        cmd.Parameters.AddWithValue("$rev", rev);
        cmd.Parameters.AddWithValue("$s", status);
        cmd.Parameters.AddWithValue("$n", notes);
        cmd.Parameters.AddWithValue("$ps", publishState);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public LocalDrawing? Get(string drawingId)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            SELECT drawing_id, project_id, number, title, rev, status, notes, publish_state
            FROM local_drawings WHERE drawing_id=$id
            """;
        cmd.Parameters.AddWithValue("$id", drawingId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return Read(r);
    }

    public IReadOnlyList<LocalDrawing> ListByProject(string projectId)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            SELECT drawing_id, project_id, number, title, rev, status, notes, publish_state
            FROM local_drawings WHERE project_id=$p ORDER BY updated_at DESC LIMIT 100
            """;
        cmd.Parameters.AddWithValue("$p", projectId);
        using var r = cmd.ExecuteReader();
        var list = new List<LocalDrawing>();
        while (r.Read()) list.Add(Read(r));
        return list;
    }

    static LocalDrawing Read(SqliteDataReader r) => new(
        r.GetString(0),
        r.GetString(1),
        r.GetString(2),
        r.GetString(3),
        r.GetString(4),
        r.GetString(5),
        r.GetString(6),
        r.GetString(7));

    public void Dispose() => _con.Dispose();
}

public sealed record LocalDrawing(
    string DrawingId,
    string ProjectId,
    string Number,
    string Title,
    string Rev,
    string Status,
    string Notes,
    string PublishState);
