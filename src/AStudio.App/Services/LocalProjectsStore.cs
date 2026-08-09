// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using Microsoft.Data.Sqlite;

namespace AStudio.App.Services;

/// <summary>
/// Practice projects in firm.db (AStudio-owned table; does not alter Aorms.Bridge schema).
/// </summary>
public sealed class LocalProjectsStore : IDisposable
{
    readonly SqliteConnection _con;

    public LocalProjectsStore(string firmDbPath)
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

    public static string DefaultFirmDbPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AStudio",
            "firm.db");

    void EnsureSchema()
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS local_projects(
              project_id TEXT PRIMARY KEY,
              project_ref TEXT NOT NULL,
              title TEXT NOT NULL,
              status TEXT NOT NULL DEFAULT 'ACTIVE',
              phase TEXT NOT NULL DEFAULT '',
              notes TEXT NOT NULL DEFAULT '',
              publish_state TEXT NOT NULL DEFAULT 'LOCAL',
              updated_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void Upsert(
        string projectId,
        string projectRef,
        string title,
        string status,
        string phase,
        string notes,
        string publishState)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            INSERT INTO local_projects(
              project_id, project_ref, title, status, phase, notes, publish_state, updated_at)
            VALUES($id,$r,$t,$s,$ph,$n,$ps,$u)
            ON CONFLICT(project_id) DO UPDATE SET
              project_ref=excluded.project_ref,
              title=excluded.title,
              status=excluded.status,
              phase=excluded.phase,
              notes=excluded.notes,
              publish_state=excluded.publish_state,
              updated_at=excluded.updated_at
            """;
        cmd.Parameters.AddWithValue("$id", projectId);
        cmd.Parameters.AddWithValue("$r", projectRef);
        cmd.Parameters.AddWithValue("$t", title);
        cmd.Parameters.AddWithValue("$s", status);
        cmd.Parameters.AddWithValue("$ph", phase);
        cmd.Parameters.AddWithValue("$n", notes);
        cmd.Parameters.AddWithValue("$ps", publishState);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public LocalProject? Get(string projectId)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            SELECT project_id, project_ref, title, status, phase, notes, publish_state
            FROM local_projects WHERE project_id=$id
            """;
        cmd.Parameters.AddWithValue("$id", projectId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return Read(r);
    }

    public IReadOnlyList<LocalProject> List()
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            SELECT project_id, project_ref, title, status, phase, notes, publish_state
            FROM local_projects ORDER BY updated_at DESC LIMIT 200
            """;
        using var r = cmd.ExecuteReader();
        var list = new List<LocalProject>();
        while (r.Read()) list.Add(Read(r));
        return list;
    }

    static LocalProject Read(SqliteDataReader r) => new(
        r.GetString(0),
        r.GetString(1),
        r.GetString(2),
        r.GetString(3),
        r.GetString(4),
        r.GetString(5),
        r.GetString(6));

    public void Dispose() => _con.Dispose();
}

public sealed record LocalProject(
    string ProjectId,
    string ProjectRef,
    string Title,
    string Status,
    string Phase,
    string Notes,
    string PublishState);
