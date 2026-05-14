using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace osafw;

/// <summary>
/// Stores ASP.NET Core session payloads in the main SQLite database for single-node deployments.
/// </summary>
public sealed class FwSqliteDistributedCache : IDistributedCache
{
    private const string TABLE_NAME = "fwsessions";
    private readonly string connstr;

    /// <summary>
    /// Creates a SQLite-backed distributed cache and ensures the session table exists.
    /// </summary>
    /// <param name="connectionString">SQLite connection string for the application database.</param>
    public FwSqliteDistributedCache(string connectionString)
    {
        connstr = connectionString;
        using var conn = openConnection();
        ensureTable(conn);
    }

    public byte[]? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        using var conn = openConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"SELECT Value, SlidingExpirationInSeconds, AbsoluteExpiration
FROM {TABLE_NAME}
WHERE Id=@id AND ExpiresAtTime>@now";
        cmd.Parameters.AddWithValue("@id", key);
        cmd.Parameters.AddWithValue("@now", format(DateTimeOffset.UtcNow));

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            removeExpired(conn, key);
            return null;
        }

        var value = (byte[])reader["Value"];
        var slidingSeconds = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1);
        var absoluteExpiration = reader.IsDBNull(2) ? null : reader.GetString(2);
        if (slidingSeconds.HasValue)
            refreshCore(conn, key, slidingSeconds.Value, absoluteExpiration);

        return value;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(Get(key));
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var now = DateTimeOffset.UtcNow;
        var absoluteExpiration = options.AbsoluteExpiration?.ToUniversalTime();
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
            absoluteExpiration = now.Add(options.AbsoluteExpirationRelativeToNow.Value);

        var slidingSeconds = options.SlidingExpiration.HasValue ? (long)Math.Ceiling(options.SlidingExpiration.Value.TotalSeconds) : (long?)null;
        var expiresAt = expirationFrom(now, absoluteExpiration, slidingSeconds);

        using var conn = openConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {TABLE_NAME}
(Id, Value, ExpiresAtTime, SlidingExpirationInSeconds, AbsoluteExpiration)
VALUES (@id, @value, @expires, @sliding, @absolute)
ON CONFLICT(Id) DO UPDATE SET
    Value=excluded.Value,
    ExpiresAtTime=excluded.ExpiresAtTime,
    SlidingExpirationInSeconds=excluded.SlidingExpirationInSeconds,
    AbsoluteExpiration=excluded.AbsoluteExpiration";
        cmd.Parameters.AddWithValue("@id", key);
        cmd.Parameters.Add("@value", SqliteType.Blob).Value = value;
        cmd.Parameters.AddWithValue("@expires", format(expiresAt));
        cmd.Parameters.AddWithValue("@sliding", slidingSeconds.HasValue ? slidingSeconds.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@absolute", absoluteExpiration.HasValue ? format(absoluteExpiration.Value) : DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Refresh(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        using var conn = openConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"SELECT SlidingExpirationInSeconds, AbsoluteExpiration
FROM {TABLE_NAME}
WHERE Id=@id AND ExpiresAtTime>@now";
        cmd.Parameters.AddWithValue("@id", key);
        cmd.Parameters.AddWithValue("@now", format(DateTimeOffset.UtcNow));

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            removeExpired(conn, key);
            return;
        }

        var slidingSeconds = reader.IsDBNull(0) ? (long?)null : reader.GetInt64(0);
        if (slidingSeconds.HasValue)
            refreshCore(conn, key, slidingSeconds.Value, reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Refresh(key);
        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        using var conn = openConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {TABLE_NAME} WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", key);
        cmd.ExecuteNonQuery();
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Remove(key);
        return Task.CompletedTask;
    }

    private SqliteConnection openConnection()
    {
        ensureDirectory();
        var conn = new SqliteConnection(connstr);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON";
        pragma.ExecuteNonQuery();
        return conn;
    }

    private void ensureDirectory()
    {
        var builder = new SqliteConnectionStringBuilder(connstr);
        var dataSource = builder.DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
            return;

        var dir = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    private static void ensureTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"CREATE TABLE IF NOT EXISTS {TABLE_NAME} (
  Id TEXT NOT NULL PRIMARY KEY,
  Value BLOB NOT NULL,
  ExpiresAtTime TEXT NOT NULL,
  SlidingExpirationInSeconds INTEGER NULL,
  AbsoluteExpiration TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_fwsessions_ExpiresAtTime ON {TABLE_NAME} (ExpiresAtTime);";
        cmd.ExecuteNonQuery();
    }

    private static void refreshCore(SqliteConnection conn, string key, long slidingSeconds, string? absoluteExpiration)
    {
        var now = DateTimeOffset.UtcNow;
        var absolute = string.IsNullOrEmpty(absoluteExpiration) ? (DateTimeOffset?)null : DateTimeOffset.Parse(absoluteExpiration, CultureInfo.InvariantCulture);
        var expiresAt = expirationFrom(now, absolute, slidingSeconds);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {TABLE_NAME} SET ExpiresAtTime=@expires WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", key);
        cmd.Parameters.AddWithValue("@expires", format(expiresAt));
        cmd.ExecuteNonQuery();
    }

    private static void removeExpired(SqliteConnection conn, string key)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {TABLE_NAME} WHERE Id=@id AND ExpiresAtTime<=@now";
        cmd.Parameters.AddWithValue("@id", key);
        cmd.Parameters.AddWithValue("@now", format(DateTimeOffset.UtcNow));
        cmd.ExecuteNonQuery();
    }

    private static DateTimeOffset expirationFrom(DateTimeOffset now, DateTimeOffset? absoluteExpiration, long? slidingSeconds)
    {
        var slidingExpiration = slidingSeconds.HasValue ? now.AddSeconds(slidingSeconds.Value) : (DateTimeOffset?)null;
        if (absoluteExpiration.HasValue && slidingExpiration.HasValue)
            return absoluteExpiration.Value < slidingExpiration.Value ? absoluteExpiration.Value : slidingExpiration.Value;

        return absoluteExpiration ?? slidingExpiration ?? DateTimeOffset.MaxValue;
    }

    private static string format(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }
}
