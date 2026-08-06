using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Othelia.Api.Observability;

public interface ITelemetryStore
{
    Task InsertSpansAsync(IReadOnlyList<SpanRecord> spans, CancellationToken ct);
    Task<IReadOnlyList<TraceRow>> QueryRecentTracesAsync(string? service, string? traceId, DateTime fromUtc, int limit, CancellationToken ct);
    Task<IReadOnlyList<SpanRecord>> QuerySpansAsync(string traceId, CancellationToken ct);
    Task<IReadOnlyList<ServiceRow>> QueryServicesAsync(DateTime fromUtc, CancellationToken ct);
}

public sealed class TelemetryStore : ITelemetryStore
{
    private readonly string _connectionString;

    public TelemetryStore(string connectionString) => _connectionString = connectionString;

    private SqlConnection OpenConnection() => new(_connectionString);

    public async Task InsertSpansAsync(IReadOnlyList<SpanRecord> spans, CancellationToken ct)
    {
        if (spans.Count == 0)
            return;

        const string prefix = @"
INSERT INTO dbo.Spans
    (TraceId, SpanId, ParentSpanId, [Name], ServiceName, Kind,
     StartTimeUtc, EndTimeUtc, DurationUs, StatusCode, StatusMessage,
     HttpMethod, HttpPath, HttpStatusCode,
     ServiceVersion, ServiceEnvironment,
     ResourceJson, AttributesJson, EventsJson, LinksJson)
VALUES ";

        var rows = new List<string>(spans.Count);
        var parameters = new DynamicParameters();
        for (var i = 0; i < spans.Count; i++)
        {
            var s = spans[i];
            rows.Add($"(@{nameof(s.TraceId)}{i}, @{nameof(s.SpanId)}{i}, @{nameof(s.ParentSpanId)}{i}, @{nameof(s.Name)}{i}, @{nameof(s.ServiceName)}{i}, @{nameof(s.Kind)}{i}, @{nameof(s.StartTimeUtc)}{i}, @{nameof(s.EndTimeUtc)}{i}, @{nameof(s.DurationUs)}{i}, @{nameof(s.StatusCode)}{i}, @{nameof(s.StatusMessage)}{i}, @{nameof(s.HttpMethod)}{i}, @{nameof(s.HttpPath)}{i}, @{nameof(s.HttpStatusCode)}{i}, @{nameof(s.ServiceVersion)}{i}, @{nameof(s.ServiceEnvironment)}{i}, @{nameof(s.ResourceJson)}{i}, @{nameof(s.AttributesJson)}{i}, @{nameof(s.EventsJson)}{i}, @{nameof(s.LinksJson)}{i})");

            parameters.Add($"TraceId{i}", s.TraceId, DbType.AnsiString, size: 32);
            parameters.Add($"SpanId{i}", s.SpanId, DbType.AnsiString, size: 16);
            parameters.Add($"ParentSpanId{i}", s.ParentSpanId, DbType.AnsiString, size: 16);
            parameters.Add($"Name{i}", s.Name, DbType.String, size: 1024);
            parameters.Add($"ServiceName{i}", s.ServiceName, DbType.String, size: 255);
            parameters.Add($"Kind{i}", s.Kind, DbType.String, size: 16);
            parameters.Add($"StartTimeUtc{i}", s.StartTimeUtc, DbType.DateTime2);
            parameters.Add($"EndTimeUtc{i}", s.EndTimeUtc, DbType.DateTime2);
            parameters.Add($"DurationUs{i}", s.DurationUs, DbType.Int64);
            parameters.Add($"StatusCode{i}", s.StatusCode, DbType.String, size: 16);
            parameters.Add($"StatusMessage{i}", s.StatusMessage, DbType.String, size: 1024);
            parameters.Add($"HttpMethod{i}", s.HttpMethod, DbType.String, size: 16);
            parameters.Add($"HttpPath{i}", s.HttpPath, DbType.String, size: 1024);
            parameters.Add($"HttpStatusCode{i}", s.HttpStatusCode, DbType.Int32);
            parameters.Add($"ServiceVersion{i}", s.ServiceVersion, DbType.String, size: 64);
            parameters.Add($"ServiceEnvironment{i}", s.ServiceEnvironment, DbType.String, size: 64);
            parameters.Add($"ResourceJson{i}", s.ResourceJson, DbType.String);
            parameters.Add($"AttributesJson{i}", s.AttributesJson, DbType.String);
            parameters.Add($"EventsJson{i}", s.EventsJson, DbType.String);
            parameters.Add($"LinksJson{i}", s.LinksJson, DbType.String);
        }

        var sql = prefix + string.Join(",\n", rows);
        await using var connection = OpenConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TraceRow>> QueryRecentTracesAsync(string? service, string? traceId, DateTime fromUtc, int limit, CancellationToken ct)
    {
        const string sql = @"
;WITH Roots AS (
    SELECT TraceId, [Name], ServiceName, HttpMethod, HttpPath, HttpStatusCode,
           ROW_NUMBER() OVER (PARTITION BY TraceId ORDER BY StartTimeUtc) AS rn
    FROM dbo.Spans
    WHERE ParentSpanId IS NULL OR ParentSpanId = N''
),
Agg AS (
    SELECT s.TraceId,
           MIN(s.StartTimeUtc) AS StartTimeUtc,
           MAX(s.EndTimeUtc) AS EndTimeUtc,
           COUNT(*) AS SpanCount,
           MAX(CASE WHEN s.StatusCode = N'Error' THEN 1 ELSE 0 END) AS HasError,
           (SELECT STRING_AGG(svc.ServiceName, N',')
              FROM (SELECT DISTINCT ServiceName FROM dbo.Spans WHERE TraceId = s.TraceId) svc) AS Tags
    FROM dbo.Spans s
    WHERE s.StartTimeUtc >= @FromUtc
      AND (@Service IS NULL OR EXISTS (SELECT 1 FROM dbo.Spans s2 WHERE s2.TraceId = s.TraceId AND s2.ServiceName = @Service))
      AND (@TraceId IS NULL OR s.TraceId = @TraceId)
    GROUP BY s.TraceId
)
SELECT a.TraceId, a.StartTimeUtc, a.EndTimeUtc, a.SpanCount, a.HasError, a.Tags,
       r.[Name] AS RootName, r.ServiceName AS RootService, r.HttpMethod, r.HttpPath, r.HttpStatusCode
FROM Agg a
LEFT JOIN Roots r ON r.TraceId = a.TraceId AND r.rn = 1
ORDER BY a.StartTimeUtc DESC
OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY;";

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<TraceRowRow>(new CommandDefinition(sql,
            new { service, traceId, FromUtc = fromUtc, Limit = limit }, cancellationToken: ct));

        return rows.Select(r => new TraceRow
        {
            TraceId = r.TraceId,
            StartTimeUtc = r.StartTimeUtc,
            DurationUs = (long)((r.EndTimeUtc - r.StartTimeUtc).TotalMilliseconds * 1000),
            SpanCount = r.SpanCount,
            HasError = r.HasError,
            RootName = r.RootName,
            RootService = r.RootService,
            HttpMethod = r.HttpMethod,
            HttpPath = r.HttpPath,
            HttpStatusCode = r.HttpStatusCode,
            Tags = r.Tags,
        }).ToList();
    }

    public async Task<IReadOnlyList<SpanRecord>> QuerySpansAsync(string traceId, CancellationToken ct)
    {
        const string sql = @"
SELECT TraceId, SpanId, ParentSpanId, [Name], ServiceName, Kind,
       StartTimeUtc, EndTimeUtc, DurationUs, StatusCode, StatusMessage,
       HttpMethod, HttpPath, HttpStatusCode,
       ServiceVersion, ServiceEnvironment,
       ResourceJson, AttributesJson, EventsJson, LinksJson
FROM dbo.Spans
WHERE TraceId = @TraceId
ORDER BY StartTimeUtc;";

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<SpanRecord>(new CommandDefinition(sql,
            new { TraceId = traceId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ServiceRow>> QueryServicesAsync(DateTime fromUtc, CancellationToken ct)
    {
        const string sql = @"
SELECT ServiceName,
       MAX(ServiceVersion) AS ServiceVersion,
       MAX(ServiceEnvironment) AS ServiceEnvironment,
       COUNT(DISTINCT TraceId) AS TotalTraces,
       SUM(CASE WHEN StatusCode = N'Error' THEN 1 ELSE 0 END) AS ErrorCount,
       MAX(StartTimeUtc) AS LastSeenUtc
FROM dbo.Spans
WHERE StartTimeUtc >= @FromUtc
GROUP BY ServiceName
ORDER BY LastSeenUtc DESC;";

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<ServiceRow>(new CommandDefinition(sql,
            new { FromUtc = fromUtc }, cancellationToken: ct));
        return rows.ToList();
    }

    private sealed class TraceRowRow
    {
        public required string TraceId { get; init; }
        public required DateTime StartTimeUtc { get; init; }
        public required DateTime EndTimeUtc { get; init; }
        public required int SpanCount { get; init; }
        public required bool HasError { get; init; }
        public string? RootName { get; init; }
        public string? RootService { get; init; }
        public string? HttpMethod { get; init; }
        public string? HttpPath { get; init; }
        public int? HttpStatusCode { get; init; }
        public string? Tags { get; init; }
    }
}
