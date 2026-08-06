using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Othelia.Api.Observability;

public interface ISettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken ct);
    Task SetAsync(string key, string value, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    Task<bool> IsAvailableAsync();
}

public sealed class SqlServerSettingsStore : ISettingsStore
{
    private readonly string _connectionString;

    public SqlServerSettingsStore(string connectionString) => _connectionString = connectionString;

    private SqlConnection OpenConnection() => new(_connectionString);

    public async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        const string sql = "SELECT [Value] FROM dbo.Settings WHERE [Key] = @Key;";
        await using var connection = OpenConnection();
        return await connection.QueryFirstOrDefaultAsync<string>(new CommandDefinition(sql,
            new { Key = key }, cancellationToken: ct));
    }

    public async Task SetAsync(string key, string value, CancellationToken ct)
    {
        const string sql = @"
MERGE dbo.Settings WITH (HOLDLOCK) AS target
USING (VALUES (@Key, @Value)) AS source ([Key], [Value])
    ON target.[Key] = source.[Key]
WHEN MATCHED THEN
    UPDATE SET [Value] = source.[Value]
WHEN NOT MATCHED THEN
    INSERT ([Key], [Value]) VALUES (source.[Key], source.[Value]);";

        await using var connection = OpenConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql,
            new { Key = key, Value = value }, cancellationToken: ct));
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        const string sql = "DELETE FROM dbo.Settings WHERE [Key] = @Key;";
        await using var connection = OpenConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql,
            new { Key = key }, cancellationToken: ct));
    }

    public Task<bool> IsAvailableAsync() => Task.FromResult(true);
}

public sealed class NoopSettingsStore : ISettingsStore
{
    public Task<string?> GetAsync(string key, CancellationToken ct) => Task.FromResult<string?>(null);
    public Task SetAsync(string key, string value, CancellationToken ct)
        => throw new InvalidOperationException("Settings persistence is not configured (no SQL storage).");
    public Task DeleteAsync(string key, CancellationToken ct)
        => throw new InvalidOperationException("Settings persistence is not configured (no SQL storage).");
    public Task<bool> IsAvailableAsync() => Task.FromResult(false);
}
