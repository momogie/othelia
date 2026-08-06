using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Othelia.Api.Observability;

public interface ITelemetryStore
{
    Task InsertSpansAsync(IReadOnlyList<SpanRecord> spans, CancellationToken ct);
    Task<IReadOnlyList<TraceRow>> QueryRecentTracesAsync(TraceQuery query, CancellationToken ct);
    Task<IReadOnlyList<SpanRecord>> QuerySpansAsync(string traceId, CancellationToken ct);
    Task<IReadOnlyList<ServiceRow>> QueryServicesAsync(DateTime fromUtc, CancellationToken ct);
    Task<DashboardAggregate> QueryDashboardAggregateAsync(DateTime fromUtc, int bucketSeconds, CancellationToken ct);
    Task<IReadOnlyList<ThroughputBucketRow>> QueryThroughputAsync(DateTime fromUtc, int bucketSeconds, CancellationToken ct);
    Task<long> DeleteSpansOlderThanAsync(DateTime olderThanUtc, CancellationToken ct);
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

        var table = new DataTable();
        table.Columns.Add("TraceId", typeof(string));
        table.Columns.Add("SpanId", typeof(string));
        table.Columns.Add("ParentSpanId", typeof(string));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("ServiceName", typeof(string));
        table.Columns.Add("Kind", typeof(string));
        table.Columns.Add("StartTimeUtc", typeof(DateTime));
        table.Columns.Add("EndTimeUtc", typeof(DateTime));
        table.Columns.Add("DurationUs", typeof(long));
        table.Columns.Add("StatusCode", typeof(string));
        table.Columns.Add("StatusMessage", typeof(string));
        table.Columns.Add("HttpMethod", typeof(string));
        table.Columns.Add("HttpPath", typeof(string));
        table.Columns.Add("HttpStatusCode", typeof(int));
        table.Columns.Add("ServiceVersion", typeof(string));
        table.Columns.Add("ServiceEnvironment", typeof(string));
        table.Columns.Add("ResourceJson", typeof(string));
        table.Columns.Add("AttributesJson", typeof(string));
        table.Columns.Add("EventsJson", typeof(string));
        table.Columns.Add("LinksJson", typeof(string));

        foreach (var s in spans)
        {
            var row = table.NewRow();
            row["TraceId"] = s.TraceId;
            row["SpanId"] = s.SpanId;
            row["ParentSpanId"] = (object?)s.ParentSpanId ?? DBNull.Value;
            row["Name"] = s.Name;
            row["ServiceName"] = s.ServiceName;
            row["Kind"] = s.Kind;
            row["StartTimeUtc"] = s.StartTimeUtc;
            row["EndTimeUtc"] = s.EndTimeUtc;
            row["DurationUs"] = s.DurationUs;
            row["StatusCode"] = s.StatusCode;
            row["StatusMessage"] = (object?)s.StatusMessage ?? DBNull.Value;
            row["HttpMethod"] = (object?)s.HttpMethod ?? DBNull.Value;
            row["HttpPath"] = (object?)s.HttpPath ?? DBNull.Value;
            row["HttpStatusCode"] = (object?)s.HttpStatusCode ?? DBNull.Value;
            row["ServiceVersion"] = (object?)s.ServiceVersion ?? DBNull.Value;
            row["ServiceEnvironment"] = (object?)s.ServiceEnvironment ?? DBNull.Value;
            row["ResourceJson"] = (object?)s.ResourceJson ?? DBNull.Value;
            row["AttributesJson"] = (object?)s.AttributesJson ?? DBNull.Value;
            row["EventsJson"] = (object?)s.EventsJson ?? DBNull.Value;
            row["LinksJson"] = (object?)s.LinksJson ?? DBNull.Value;
            table.Rows.Add(row);
        }

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);
        using var bulk = new SqlBulkCopy(connection)
        {
            DestinationTableName = "dbo.Spans",
            BatchSize = 1000,
        };
        foreach (DataColumn column in table.Columns)
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        await bulk.WriteToServerAsync(table, ct);
    }

    public async Task<IReadOnlyList<TraceRow>> QueryRecentTracesAsync(TraceQuery query, CancellationToken ct)
    {
        const string baseSql = @"
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
           DATEDIFF(MICROSECOND, MIN(s.StartTimeUtc), MAX(s.EndTimeUtc)) AS DurationUs,
           COUNT(*) AS SpanCount,
           MAX(CASE WHEN s.StatusCode = N'Error' THEN 1 ELSE 0 END) AS HasError,
           (SELECT STRING_AGG(svc.ServiceName, N',')
              FROM (SELECT DISTINCT ServiceName FROM dbo.Spans WHERE TraceId = s.TraceId) svc) AS Tags
    FROM dbo.Spans s
    WHERE s.StartTimeUtc >= @FromUtc
      AND (@ToUtc IS NULL OR s.StartTimeUtc <= @ToUtc)
      AND (@Service IS NULL OR EXISTS (SELECT 1 FROM dbo.Spans s2 WHERE s2.TraceId = s.TraceId AND s2.ServiceName = @Service))
      AND (@TraceId IS NULL OR s.TraceId = @TraceId)
    GROUP BY s.TraceId
)
SELECT a.TraceId, a.StartTimeUtc, a.EndTimeUtc, a.DurationUs, a.SpanCount, a.HasError, a.Tags,
       r.[Name] AS RootName, r.ServiceName AS RootService, r.HttpMethod, r.HttpPath, r.HttpStatusCode,
       COUNT(*) OVER () AS Total
FROM Agg a
LEFT JOIN Roots r ON r.TraceId = a.TraceId AND r.rn = 1";

        var where = new List<string>();
        var parameters = new DynamicParameters();

        parameters.Add("FromUtc", query.FromUtc ?? DateTime.UtcNow.AddHours(-1), DbType.DateTime2);
        parameters.Add("ToUtc", query.ToUtc, DbType.DateTime2);
        parameters.Add("Service", query.Service);
        parameters.Add("TraceId", query.TraceId);

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            where.Add("r.[Name] LIKE @NamePattern ESCAPE N'~'");
            parameters.Add("NamePattern", $"%{EscapeLike(query.Name)}%");
        }
        if (!string.IsNullOrWhiteSpace(query.NameNot))
        {
            where.Add("r.[Name] NOT LIKE @NameNotPattern ESCAPE N'~'");
            parameters.Add("NameNotPattern", $"%{EscapeLike(query.NameNot)}%");
        }
        if (!string.IsNullOrWhiteSpace(query.Path))
        {
            where.Add("r.HttpPath LIKE @PathPattern ESCAPE N'~'");
            parameters.Add("PathPattern", $"%{EscapeLike(query.Path)}%");
        }
        if (!string.IsNullOrWhiteSpace(query.PathNot))
        {
            where.Add("r.HttpPath NOT LIKE @PathNotPattern ESCAPE N'~'");
            parameters.Add("PathNotPattern", $"%{EscapeLike(query.PathNot)}%");
        }
        if (query.MinDurationMs.HasValue)
        {
            where.Add("a.DurationUs >= @MinDurationUs");
            parameters.Add("MinDurationUs", query.MinDurationMs.Value * 1000L);
        }
        if (query.MaxDurationMs.HasValue)
        {
            where.Add("a.DurationUs <= @MaxDurationUs");
            parameters.Add("MaxDurationUs", query.MaxDurationMs.Value * 1000L);
        }

        switch (query.Status?.ToLowerInvariant())
        {
            case "error":
                where.Add("a.HasError = 1");
                break;
            case "ok":
                where.Add("a.HasError = 0");
                break;
            case "slow":
                where.Add("a.DurationUs >= @SlowThresholdUs");
                parameters.Add("SlowThresholdUs", (query.SlowThresholdMs ?? 1000) * 1000L);
                break;
        }

        var orderBy = query.Sort?.ToLowerInvariant() switch
        {
            "start_asc" => "a.StartTimeUtc ASC",
            "start_desc" => "a.StartTimeUtc DESC",
            "duration_asc" => "a.DurationUs ASC",
            "duration_desc" => "a.DurationUs DESC",
            _ => "a.StartTimeUtc DESC",
        };

        parameters.Add("Offset", Math.Max(0, query.Offset));
        parameters.Add("Limit", Math.Clamp(query.Limit, 1, 1000));

        var sql = baseSql;
        if (where.Count > 0)
            sql += "\nWHERE " + string.Join("\n  AND ", where);
        sql += $"\nORDER BY {orderBy}\nOFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY;";

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<TraceRowRow>(new CommandDefinition(sql, parameters, cancellationToken: ct));

        return rows.Select(r => new TraceRow
        {
            TraceId = r.TraceId,
            StartTimeUtc = r.StartTimeUtc,
            DurationUs = r.DurationUs,
            SpanCount = r.SpanCount,
            HasError = r.HasError,
            RootName = r.RootName,
            RootService = r.RootService,
            HttpMethod = r.HttpMethod,
            HttpPath = r.HttpPath,
            HttpStatusCode = r.HttpStatusCode,
            Tags = r.Tags,
            Total = r.Total,
        }).ToList();
    }

    public async Task<long> DeleteSpansOlderThanAsync(DateTime olderThanUtc, CancellationToken ct)
    {
        const string sql = "DELETE FROM dbo.Spans WHERE StartTimeUtc < @OlderThanUtc;";
        await using var connection = OpenConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql,
            new { OlderThanUtc = olderThanUtc }, cancellationToken: ct));
    }

    private static string EscapeLike(string value) => value
        .Replace("~", "~~")
        .Replace("%", "~%")
        .Replace("_", "~_")
        .Replace("[", "~[");

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

    public async Task<DashboardAggregate> QueryDashboardAggregateAsync(
        DateTime fromUtc, int bucketSeconds, CancellationToken ct)
    {
        const string totalsSql = @"
;WITH Agg AS (
    SELECT TraceId,
           MAX(CASE WHEN StatusCode = N'Error' THEN 1 ELSE 0 END) AS HasError
    FROM dbo.Spans
    WHERE StartTimeUtc >= @FromUtc
    GROUP BY TraceId
),
Totals AS (
    SELECT COUNT(*) AS TotalTraces,
           COALESCE(SUM(CASE WHEN HasError = 1 THEN 1 ELSE 0 END), 0) AS ErrorTraces
    FROM Agg
)
SELECT t.TotalTraces, t.ErrorTraces, s.TotalSpans
FROM Totals t
CROSS JOIN (SELECT COUNT(*) AS TotalSpans FROM dbo.Spans WHERE StartTimeUtc >= @FromUtc) s;";

        const string percentileSql = @"
;WITH Agg AS (
    SELECT DATEDIFF(MICROSECOND, MIN(StartTimeUtc), MAX(EndTimeUtc)) AS DurationUs
    FROM dbo.Spans
    WHERE StartTimeUtc >= @FromUtc
    GROUP BY TraceId
)
SELECT DISTINCT
       PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY DurationUs) OVER () AS P50Us,
       PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY DurationUs) OVER () AS P99Us
FROM Agg;";

        const string servicesSql = @"
;WITH Stats AS (
    SELECT ServiceName,
           MAX(ServiceVersion) AS ServiceVersion,
           MAX(ServiceEnvironment) AS ServiceEnvironment,
           COUNT(DISTINCT TraceId) AS TotalTraces,
           COUNT(*) AS TotalSpans,
           SUM(CASE WHEN StatusCode = N'Error' THEN 1 ELSE 0 END) AS ErrorSpans,
           MAX(StartTimeUtc) AS LastSeenUtc
    FROM dbo.Spans
    WHERE StartTimeUtc >= @FromUtc
    GROUP BY ServiceName
),
Pct AS (
    SELECT DISTINCT ServiceName,
           PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY DurationUs) OVER (PARTITION BY ServiceName) AS P99Us
    FROM dbo.Spans
    WHERE StartTimeUtc >= @FromUtc
)
SELECT s.ServiceName, s.ServiceVersion, s.ServiceEnvironment,
       s.TotalTraces, s.TotalSpans, s.ErrorSpans, s.LastSeenUtc, p.P99Us
FROM Stats s
LEFT JOIN Pct p ON p.ServiceName = s.ServiceName
ORDER BY s.LastSeenUtc DESC;";

        await using var connection = OpenConnection();

        var totals = await connection.QueryFirstOrDefaultAsync<TotalsRow>(new CommandDefinition(totalsSql,
            new { FromUtc = fromUtc }, cancellationToken: ct));

        var percentile = await connection.QueryFirstOrDefaultAsync<PercentileRow>(new CommandDefinition(percentileSql,
            new { FromUtc = fromUtc }, cancellationToken: ct));

        var throughput = await QueryThroughputAsync(fromUtc, bucketSeconds, ct);

        var services = await connection.QueryAsync<ServiceStatsSqlRow>(new CommandDefinition(servicesSql,
            new { FromUtc = fromUtc }, cancellationToken: ct));

        return new DashboardAggregate
        {
            TotalTraces = totals?.TotalTraces ?? 0,
            TotalSpans = totals?.TotalSpans ?? 0,
            ErrorTraces = totals?.ErrorTraces ?? 0,
            P50Us = percentile?.P50Us,
            P99Us = percentile?.P99Us,
            Throughput = throughput,
            ServiceStats = services.Select(s => new ServiceStatsRow
            {
                ServiceName = s.ServiceName,
                ServiceVersion = s.ServiceVersion,
                ServiceEnvironment = s.ServiceEnvironment,
                TotalTraces = s.TotalTraces,
                TotalSpans = s.TotalSpans,
                ErrorSpans = s.ErrorSpans,
                LastSeenUtc = s.LastSeenUtc,
                P99Us = s.P99Us,
            }).ToList(),
        };
    }

    public async Task<IReadOnlyList<ThroughputBucketRow>> QueryThroughputAsync(
        DateTime fromUtc, int bucketSeconds, CancellationToken ct)
    {
        const string sql = @"
;WITH Spans AS (
    SELECT TraceId,
           DATEDIFF(SECOND, @FromUtc, StartTimeUtc) / @BucketSeconds AS BucketIndex
    FROM dbo.Spans
    WHERE StartTimeUtc >= @FromUtc
)
SELECT BucketIndex, COUNT(DISTINCT TraceId) AS [Count]
FROM Spans
GROUP BY BucketIndex
ORDER BY BucketIndex;";

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<ThroughputBucketRow>(new CommandDefinition(sql,
            new { FromUtc = fromUtc, BucketSeconds = Math.Max(1, bucketSeconds) }, cancellationToken: ct));
        return rows.ToList();
    }

    private sealed class TraceRowRow
    {
        public required string TraceId { get; init; }
        public required DateTime StartTimeUtc { get; init; }
        public required DateTime EndTimeUtc { get; init; }
        public required long DurationUs { get; init; }
        public required int SpanCount { get; init; }
        public required bool HasError { get; init; }
        public string? RootName { get; init; }
        public string? RootService { get; init; }
        public string? HttpMethod { get; init; }
        public string? HttpPath { get; init; }
        public int? HttpStatusCode { get; init; }
        public string? Tags { get; init; }
        public required int Total { get; init; }
    }

    private sealed class TotalsRow
    {
        public required long TotalTraces { get; init; }
        public required long TotalSpans { get; init; }
        public required long ErrorTraces { get; init; }
    }

    private sealed class PercentileRow
    {
        public double? P50Us { get; init; }
        public double? P99Us { get; init; }
    }

    private sealed class ServiceStatsSqlRow
    {
        public required string ServiceName { get; init; }
        public string? ServiceVersion { get; init; }
        public string? ServiceEnvironment { get; init; }
        public required long TotalTraces { get; init; }
        public required long TotalSpans { get; init; }
        public required long ErrorSpans { get; init; }
        public required DateTime LastSeenUtc { get; init; }
        public double? P99Us { get; init; }
    }
}
