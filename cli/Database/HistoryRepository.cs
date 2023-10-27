using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Elk.Cli.Database;

class HistoryRepository : IDisposable
{
    private readonly int _maxEntries;
    private readonly SqliteConnection _db;

    public HistoryRepository(int maxEntries)
    {
        _maxEntries = maxEntries;

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "elk"
        );
        Directory.CreateDirectory(path);

        var dbPath = Path.Combine(path, "history.db");
        _db = new SqliteConnection($"Data Source={dbPath}");
        _db.Open();

        var createTableCommand = _db.CreateCommand();
        createTableCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS HistoryEntry(
                id INTEGER PRIMARY KEY,
                path TEXT,
                content TEXT,
                time DATE
            );
            CREATE INDEX IF NOT EXISTS idx_HistoryEntry ON HistoryEntry (path);
            CREATE INDEX IF NOT EXISTS idx_HistoryEntry ON HistoryEntry (content);
            CREATE INDEX IF NOT EXISTS idx_HistoryEntry ON HistoryEntry (time);
        """;
        createTableCommand.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    public List<HistoryEntry> GetAll()
    {
        var command = _db.CreateCommand();
        command.CommandText = """
            SELECT path, content, time
            FROM HistoryEntry
            ORDER BY time ASC
            LIMIT 250;
        """;

        var entries = new List<HistoryEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var entry = new HistoryEntry
            {
                Path = reader.GetString(0),
                Content = reader.GetString(1),
                Time = reader.GetDateTime(2),
            };
            entries.Add(entry);
        }

        return entries;
    }

    public HistoryEntry? GetSingleWithPathAndStart(string path, string start)
    {
        var command = _db.CreateCommand();
        command.CommandText = """
            SELECT path, content, time
            FROM HistoryEntry
            WHERE path LIKE $path
                AND content LIKE $start || '%'
            ORDER BY time DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$start", start);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new HistoryEntry
            {
                Path = reader.GetString(0),
                Content = reader.GetString(1),
                Time = reader.GetDateTime(2),
            };
        }

        return null;
    }

    public List<HistoryEntry> GetWithStart(string start)
    {
        var command = _db.CreateCommand();
        command.CommandText = """
            SELECT path, content, time
            FROM HistoryEntry
            WHERE content LIKE $start || '%'
            ORDER BY time ASC
            LIMIT 50;
        """;
        command.Parameters.AddWithValue("$start", start);

        var entries = new List<HistoryEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var entry = new HistoryEntry
            {
                Path = reader.GetString(0),
                Content = reader.GetString(1),
                Time = reader.GetDateTime(2),
            };
            entries.Add(entry);
        }

        return entries;
    }

    public void Add(HistoryEntry entry)
    {
        var command = _db.CreateCommand();
        command.CommandText = $"""
            INSERT INTO HistoryEntry (path, content, time)
            VALUES ($path, $content, $time);

            DELETE FROM HistoryEntry
            WHERE Id IN
               (SELECT Id FROM HistoryEntry
                ORDER BY Time
                LIMIT CASE
                    WHEN (SELECT MAX(rowid) FROM HistoryEntry) - (SELECT MIN(rowid) FROM HistoryEntry) > {_maxEntries}
                        THEN {Math.Max(2, _maxEntries / 12)}
                        ELSE 0
                    END);
        """;
        command.Parameters.AddWithValue("$path", entry.Path);
        command.Parameters.AddWithValue("$content", entry.Content);
        command.Parameters.AddWithValue("$time", entry.Time);
        command.ExecuteNonQuery();
    }
}