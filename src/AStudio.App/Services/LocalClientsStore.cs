// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using Microsoft.Data.Sqlite;

namespace AStudio.App.Services;

/// <summary>Thin clients directory in firm.db (AStudio chrome parity).</summary>
public sealed class LocalClientsStore : IDisposable
{
    readonly SqliteConnection _con;

    public LocalClientsStore(string firmDbPath)
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
            CREATE TABLE IF NOT EXISTS local_clients(
              client_id TEXT PRIMARY KEY,
              name TEXT NOT NULL,
              contact TEXT NOT NULL DEFAULT '',
              email TEXT NOT NULL DEFAULT '',
              notes TEXT NOT NULL DEFAULT '',
              publish_state TEXT NOT NULL DEFAULT 'LOCAL',
              updated_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void Upsert(
        string clientId,
        string name,
        string contact,
        string email,
        string notes,
        string publishState)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            INSERT INTO local_clients(
              client_id, name, contact, email, notes, publish_state, updated_at)
            VALUES($id,$n,$c,$e,$notes,$ps,$u)
            ON CONFLICT(client_id) DO UPDATE SET
              name=excluded.name,
              contact=excluded.contact,
              email=excluded.email,
              notes=excluded.notes,
              publish_state=excluded.publish_state,
              updated_at=excluded.updated_at
            """;
        cmd.Parameters.AddWithValue("$id", clientId);
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$c", contact);
        cmd.Parameters.AddWithValue("$e", email);
        cmd.Parameters.AddWithValue("$notes", notes);
        cmd.Parameters.AddWithValue("$ps", publishState);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public LocalClient? Get(string clientId)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            SELECT client_id, name, contact, email, notes, publish_state
            FROM local_clients WHERE client_id=$id
            """;
        cmd.Parameters.AddWithValue("$id", clientId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return Read(r);
    }

    public IReadOnlyList<LocalClient> List()
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = """
            SELECT client_id, name, contact, email, notes, publish_state
            FROM local_clients ORDER BY updated_at DESC LIMIT 200
            """;
        using var r = cmd.ExecuteReader();
        var list = new List<LocalClient>();
        while (r.Read()) list.Add(Read(r));
        return list;
    }

    public int Count()
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM local_clients";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    static LocalClient Read(SqliteDataReader r) => new(
        r.GetString(0),
        r.GetString(1),
        r.GetString(2),
        r.GetString(3),
        r.GetString(4),
        r.GetString(5));

    public void Dispose() => _con.Dispose();
}

public sealed record LocalClient(
    string ClientId,
    string Name,
    string Contact,
    string Email,
    string Notes,
    string PublishState);
