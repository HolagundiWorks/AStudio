// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using Microsoft.Data.Sqlite;

namespace AStudio.App.Services;

/// <summary>COA fee / invoice stubs in firm.db — amounts in integer paise (S3).</summary>
public sealed class LocalFeesStore : IDisposable
{
    readonly SqliteConnection _con;

    public LocalFeesStore(string firmDbPath)
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
            CREATE TABLE IF NOT EXISTS local_fees(
              fee_id TEXT PRIMARY KEY,
              project_id TEXT NOT NULL,
              title TEXT NOT NULL,
              amount_paise INTEGER NOT NULL DEFAULT 0,
              status TEXT NOT NULL DEFAULT 'DRAFT',
              notes TEXT NOT NULL DEFAULT '',
              publish_state TEXT NOT NULL DEFAULT 'LOCAL',
              updated_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_local_fees_project ON local_fees(project_id);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Upsert(
        string feeId,
        string projectId,
        string title,
        long amountPaise,
        string status,
        string notes,
        string publishState)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            INSERT INTO local_fees(
              fee_id, project_id, title, amount_paise, status, notes, publish_state, updated_at)
            VALUES($id,$p,$t,$a,$s,$n,$ps,$u)
            ON CONFLICT(fee_id) DO UPDATE SET
              project_id=excluded.project_id,
              title=excluded.title,
              amount_paise=excluded.amount_paise,
              status=excluded.status,
              notes=excluded.notes,
              publish_state=excluded.publish_state,
              updated_at=excluded.updated_at
            """;
        cmd.Parameters.AddWithValue("$id", feeId);
        cmd.Parameters.AddWithValue("$p", projectId);
        cmd.Parameters.AddWithValue("$t", title);
        cmd.Parameters.AddWithValue("$a", amountPaise);
        cmd.Parameters.AddWithValue("$s", status);
        cmd.Parameters.AddWithValue("$n", notes);
        cmd.Parameters.AddWithValue("$ps", publishState);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public LocalFee? Get(string feeId)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            SELECT fee_id, project_id, title, amount_paise, status, notes, publish_state
            FROM local_fees WHERE fee_id=$id
            """;
        cmd.Parameters.AddWithValue("$id", feeId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return Read(r);
    }

    public IReadOnlyList<LocalFee> ListByProject(string projectId)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            SELECT fee_id, project_id, title, amount_paise, status, notes, publish_state
            FROM local_fees WHERE project_id=$p ORDER BY updated_at DESC LIMIT 100
            """;
        cmd.Parameters.AddWithValue("$p", projectId);
        using var r = cmd.ExecuteReader();
        var list = new List<LocalFee>();
        while (r.Read()) list.Add(Read(r));
        return list;
    }

    static LocalFee Read(SqliteDataReader r) => new(
        r.GetString(0),
        r.GetString(1),
        r.GetString(2),
        r.GetInt64(3),
        r.GetString(4),
        r.GetString(5),
        r.GetString(6));

    public void Dispose() => _con.Dispose();
}

public sealed record LocalFee(
    string FeeId,
    string ProjectId,
    string Title,
    long AmountPaise,
    string Status,
    string Notes,
    string PublishState);
