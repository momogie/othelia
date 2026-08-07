using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Othelia.Api.Observability;

public interface ITelemetryStore
{
    Task InsertSpansAsync(IReadOnlyList<SpanRecord> spans, CancellationToken ct);
    Task<IReadOnlyList<TraceRow>> QueryRecentTracesAsync(TraceQuery query, CancellationToken ct);
    Task<IReadOnlyList<SpanRecord>> QuerySpansAsync(string traceId, int maxRows, CancellationToken ct);
    Task<IReadOnlyList<ServiceRow>> QueryServicesAsync(DateTime fromUtc, CancellationToken ct);
    Task<DashboardAggregate> QueryDashboardAggregateAsync(DateTime fromUtc, int bucketSeconds, string? service, CancellationToken ct);
    Task<IReadOnlyList<ThroughputBucketRow>> QueryThroughputAsync(DateTime fromUtc, int bucketSeconds, string? service, CancellationToken ct);
    Task<long> DeleteSpansOlderThanAsync(DateTime olderThanUtc, CancellationToken ct);
    Task InsertLogsAsync(IReadOnlyList<LogRecord> logs, CancellationToken ct);
    Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery query, CancellationToken ct);
    Task<long> DeleteLogsOlderThanAsync(DateTime olderThanUtc, CancellationToken ct);
    Task<bool> IsHealthyAsync(CancellationToken ct);
    Task InsertMetricsAsync(IReadOnlyList<MetricRecord> metrics, CancellationToken ct);
    Task<IReadOnlyList<MetricNameRow>> QueryMetricNamesAsync(DateTime fromUtc, string? service, CancellationToken ct);
    Task<IReadOnlyList<MetricPointRow>> QueryMetricPointsAsync(string metricName, string? service, MetricQuery query, CancellationToken ct);
    Task<long> DeleteMetricsOlderThanAsync(DateTime olderThanUtc, CancellationToken ct);
    Task<ServiceMapResult> QueryServiceMapAsync(DateTime fromUtc, string? service, CancellationToken ct);
    Task<IReadOnlyList<CollectorRow>> QueryCollectorsAsync(DateTime fromUtc, CancellationToken ct);
    Task<IReadOnlyList<AlertStateRow>> GetAlertStatesAsync(CancellationToken ct);
    Task AcknowledgeAlertAsync(string alertKey, CancellationToken ct);
    Task<IReadOnlyList<ExpensiveQueryRow>> QueryExpensiveQueriesAsync(ExpensiveQueryQuery query, CancellationToken ct);
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
           DATEDIFF_BIG(MICROSECOND, MIN(s.StartTimeUtc), MAX(s.EndTimeUtc)) AS DurationUs,
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

        if (!string.IsNullOrWhiteSpace(query.Attributes))
        {
            var (key, value) = SplitAttribute(query.Attributes);
            var attrKey = EscapeLike(key);
            if (string.IsNullOrEmpty(value))
            {
                where.Add("EXISTS (SELECT 1 FROM dbo.Spans s3 WHERE s3.TraceId = a.TraceId AND s3.AttributesJson LIKE @AttrPattern ESCAPE N'~')");
                parameters.Add("AttrPattern", $"%\"{attrKey}\":%");
            }
            else
            {
                var attrValue = EscapeLike(value);
                where.Add(@"EXISTS (SELECT 1 FROM dbo.Spans s3 WHERE s3.TraceId = a.TraceId
                    AND (s3.AttributesJson LIKE @AttrPatternQuoted ESCAPE N'~'
                         OR s3.AttributesJson LIKE @AttrPatternRaw ESCAPE N'~'))");
                parameters.Add("AttrPatternQuoted", $"%\"{attrKey}\":\"{attrValue}\"%");
                parameters.Add("AttrPatternRaw", $"%\"{attrKey}\":{attrValue}%");
            }
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
        const string sql = "DELETE TOP (@BatchSize) FROM dbo.Spans WHERE StartTimeUtc < @OlderThanUtc;";
        await using var connection = OpenConnection();
        const int batch = 5000;
        long total = 0;
        while (!ct.IsCancellationRequested)
        {
            var deleted = await connection.ExecuteAsync(new CommandDefinition(sql,
                new { OlderThanUtc = olderThanUtc, BatchSize = batch }, cancellationToken: ct));
            total += deleted;
            if (deleted < batch)
                break;
        }
        return total;
    }

    public async Task InsertLogsAsync(IReadOnlyList<LogRecord> logs, CancellationToken ct)
    {
        if (logs.Count == 0)
            return;

        var table = new DataTable();
        table.Columns.Add("TimestampUtc", typeof(DateTime));
        table.Columns.Add("ServiceName", typeof(string));
        table.Columns.Add("ServiceVersion", typeof(string));
        table.Columns.Add("ServiceEnvironment", typeof(string));
        table.Columns.Add("SeverityText", typeof(string));
        table.Columns.Add("SeverityNumber", typeof(int));
        table.Columns.Add("Body", typeof(string));
        table.Columns.Add("TraceId", typeof(string));
        table.Columns.Add("SpanId", typeof(string));
        table.Columns.Add("AttributesJson", typeof(string));
        table.Columns.Add("ResourceJson", typeof(string));

        foreach (var log in logs)
        {
            var row = table.NewRow();
            row["TimestampUtc"] = log.TimestampUtc;
            row["ServiceName"] = log.ServiceName;
            row["ServiceVersion"] = (object?)log.ServiceVersion ?? DBNull.Value;
            row["ServiceEnvironment"] = (object?)log.ServiceEnvironment ?? DBNull.Value;
            row["SeverityText"] = log.SeverityText;
            row["SeverityNumber"] = log.SeverityNumber;
            row["Body"] = (object?)log.Body ?? DBNull.Value;
            row["TraceId"] = (object?)log.TraceId ?? DBNull.Value;
            row["SpanId"] = (object?)log.SpanId ?? DBNull.Value;
            row["AttributesJson"] = (object?)log.AttributesJson ?? DBNull.Value;
            row["ResourceJson"] = (object?)log.ResourceJson ?? DBNull.Value;
            table.Rows.Add(row);
        }

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);
        using var bulk = new SqlBulkCopy(connection)
        {
            DestinationTableName = "dbo.Logs",
            BatchSize = 1000,
        };
        foreach (DataColumn column in table.Columns)
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        await bulk.WriteToServerAsync(table, ct);
    }

    public async Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery query, CancellationToken ct)
    {
        const string baseSql = @"
SELECT TimestampUtc, ServiceName, ServiceVersion, ServiceEnvironment,
       SeverityText, SeverityNumber, Body, TraceId, SpanId, AttributesJson,
       COUNT(*) OVER () AS Total
FROM dbo.Logs";

        var where = new List<string> { "TimestampUtc >= @FromUtc" };
        var parameters = new DynamicParameters();

        parameters.Add("FromUtc", query.FromUtc ?? DateTime.UtcNow.AddHours(-1), DbType.DateTime2);
        parameters.Add("ToUtc", query.ToUtc, DbType.DateTime2);
        parameters.Add("Service", query.Service);
        parameters.Add("TraceId", query.TraceId);

        if (query.ToUtc.HasValue)
            where.Add("TimestampUtc <= @ToUtc");
        if (!string.IsNullOrWhiteSpace(query.Service))
            where.Add("ServiceName = @Service");
        if (!string.IsNullOrWhiteSpace(query.TraceId))
            where.Add("TraceId = @TraceId");

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add("(Body LIKE @SearchPattern ESCAPE N'~' OR AttributesJson LIKE @SearchPattern ESCAPE N'~')");
            parameters.Add("SearchPattern", $"%{EscapeLike(query.Search)}%");
        }

        if (!string.IsNullOrWhiteSpace(query.Severity))
        {
            var minNumber = query.Severity.ToLowerInvariant() switch
            {
                "error" or "fatal" or "critical" => 17,
                "warn" or "warning" => 13,
                _ => (int?)null,
            };
            if (minNumber.HasValue)
            {
                where.Add("SeverityNumber >= @MinSeverityNumber");
                parameters.Add("MinSeverityNumber", minNumber.Value);
            }
        }

        var orderBy = query.Sort?.ToLowerInvariant() switch
        {
            "start_asc" => "TimestampUtc ASC",
            "start_desc" => "TimestampUtc DESC",
            _ => "TimestampUtc DESC",
        };

        parameters.Add("Offset", Math.Max(0, query.Offset));
        parameters.Add("Limit", Math.Clamp(query.Limit, 1, 1000));

        var sql = baseSql
            + "\nWHERE " + string.Join("\n  AND ", where)
            + $"\nORDER BY {orderBy}\nOFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY;";

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<LogRowSql>(new CommandDefinition(sql, parameters, cancellationToken: ct));

        return rows.Select(r => new LogRow
        {
            TimestampUtc = r.TimestampUtc,
            ServiceName = r.ServiceName,
            ServiceVersion = r.ServiceVersion,
            ServiceEnvironment = r.ServiceEnvironment,
            SeverityText = r.SeverityText,
            SeverityNumber = r.SeverityNumber,
            Body = r.Body,
            TraceId = r.TraceId,
            SpanId = r.SpanId,
            AttributesJson = r.AttributesJson,
            Total = r.Total,
        }).ToList();
    }

    public async Task<long> DeleteLogsOlderThanAsync(DateTime olderThanUtc, CancellationToken ct)
    {
        const string sql = "DELETE TOP (@BatchSize) FROM dbo.Logs WHERE TimestampUtc < @OlderThanUtc;";
        await using var connection = OpenConnection();
        const int batch = 5000;
        long total = 0;
        while (!ct.IsCancellationRequested)
        {
            var deleted = await connection.ExecuteAsync(new CommandDefinition(sql,
                new { OlderThanUtc = olderThanUtc, BatchSize = batch }, cancellationToken: ct));
            total += deleted;
            if (deleted < batch)
                break;
        }
        return total;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        try
        {
            await using var connection = OpenConnection();
            await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1;", cancellationToken: ct));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string EscapeLike(string value) => value
        .Replace("~", "~~")
        .Replace("%", "~%")
        .Replace("_", "~_")
        .Replace("[", "~[");

    private static (string Key, string? Value) SplitAttribute(string attributes)
    {
        var index = attributes.IndexOf('=');
        if (index <= 0)
            return (attributes.Trim(), null);

        var key = attributes[..index].Trim();
        var value = attributes[(index + 1)..].Trim();
        return (key, value.Length == 0 ? null : value);
    }

    public async Task<IReadOnlyList<SpanRecord>> QuerySpansAsync(string traceId, int maxRows, CancellationToken ct)
    {
        const string sql = @"
SELECT TOP (@MaxRows) TraceId, SpanId, ParentSpanId, [Name], ServiceName, Kind,
       StartTimeUtc, EndTimeUtc, DurationUs, StatusCode, StatusMessage,
       HttpMethod, HttpPath, HttpStatusCode,
       ServiceVersion, ServiceEnvironment,
       ResourceJson, AttributesJson, EventsJson, LinksJson
FROM dbo.Spans
WHERE TraceId = @TraceId
ORDER BY StartTimeUtc;";

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<SpanRecord>(new CommandDefinition(sql,
            new { TraceId = traceId, MaxRows = Math.Max(1, maxRows) }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task InsertMetricsAsync(IReadOnlyList<MetricRecord> metrics, CancellationToken ct)
    {
        if (metrics.Count == 0)
            return;

        var table = new DataTable();
        table.Columns.Add("TimestampUtc", typeof(DateTime));
        table.Columns.Add("ServiceName", typeof(string));
        table.Columns.Add("ServiceVersion", typeof(string));
        table.Columns.Add("ServiceEnvironment", typeof(string));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Type", typeof(string));
        table.Columns.Add("Unit", typeof(string));
        table.Columns.Add("Value", typeof(double));
        table.Columns.Add("Count", typeof(long));
        table.Columns.Add("AttributesJson", typeof(string));
        table.Columns.Add("ResourceJson", typeof(string));

        foreach (var m in metrics)
        {
            var row = table.NewRow();
            row["TimestampUtc"] = m.TimestampUtc;
            row["ServiceName"] = m.ServiceName;
            row["ServiceVersion"] = (object?)m.ServiceVersion ?? DBNull.Value;
            row["ServiceEnvironment"] = (object?)m.ServiceEnvironment ?? DBNull.Value;
            row["Name"] = m.Name;
            row["Type"] = m.Type;
            row["Unit"] = (object?)m.Unit ?? DBNull.Value;
            row["Value"] = m.Value;
            row["Count"] = (object?)m.Count ?? DBNull.Value;
            row["AttributesJson"] = (object?)m.AttributesJson ?? DBNull.Value;
            row["ResourceJson"] = (object?)m.ResourceJson ?? DBNull.Value;
            table.Rows.Add(row);
        }

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);
        using var bulk = new SqlBulkCopy(connection)
        {
            DestinationTableName = "dbo.Metrics",
            BatchSize = 1000,
        };
        foreach (DataColumn column in table.Columns)
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        await bulk.WriteToServerAsync(table, ct);
    }

    public async Task<IReadOnlyList<MetricNameRow>> QueryMetricNamesAsync(
        DateTime fromUtc, string? service, CancellationToken ct)
    {
        const string sql = @"
SELECT [Name], MAX(Unit) AS Unit, COUNT(*) AS DataPoints, MAX(TimestampUtc) AS LastSeenUtc
FROM dbo.Metrics
WHERE TimestampUtc >= @FromUtc
  AND (@Service IS NULL OR ServiceName = @Service)
GROUP BY [Name]
ORDER BY LastSeenUtc DESC;";

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<MetricNameRow>(new CommandDefinition(sql,
            new { FromUtc = fromUtc, Service = service }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MetricPointRow>> QueryMetricPointsAsync(
        string metricName, string? service, MetricQuery query, CancellationToken ct)
    {
        var bucketSeconds = Math.Max(1, query.BucketSeconds);
        var bucketSql = query.Aggregation.ToLowerInvariant() switch
        {
            "min" => "MIN(Value)",
            "max" => "MAX(Value)",
            "sum" => "SUM(Value)",
            "last" => null,
            _ => "AVG(Value)",
        };

        const string aggregateTemplate = @"
;WITH Buckets AS (
    SELECT DATEDIFF(SECOND, @FromUtc, TimestampUtc) / @BucketSeconds AS BucketIndex,
           TimestampUtc, Value
    FROM dbo.Metrics
    WHERE [Name] = @Name
      AND TimestampUtc >= @FromUtc
      AND (@ToUtc IS NULL OR TimestampUtc <= @ToUtc)
      AND (@Service IS NULL OR ServiceName = @Service)
)
SELECT BucketIndex, DATEADD(SECOND, BucketIndex * @BucketSeconds, @FromUtc) AS TimestampUtc, {expr} AS Value
FROM Buckets
GROUP BY BucketIndex
ORDER BY BucketIndex;";

        const string lastTemplate = @"
;WITH Buckets AS (
    SELECT DATEDIFF(SECOND, @FromUtc, TimestampUtc) / @BucketSeconds AS BucketIndex,
           TimestampUtc, Value
    FROM dbo.Metrics
    WHERE [Name] = @Name
      AND TimestampUtc >= @FromUtc
      AND (@ToUtc IS NULL OR TimestampUtc <= @ToUtc)
      AND (@Service IS NULL OR ServiceName = @Service)
),
Ranked AS (
    SELECT BucketIndex, TimestampUtc, Value,
           ROW_NUMBER() OVER (PARTITION BY BucketIndex ORDER BY TimestampUtc DESC) AS rn
    FROM Buckets
)
SELECT BucketIndex, DATEADD(SECOND, BucketIndex * @BucketSeconds, @FromUtc) AS TimestampUtc, Value
FROM Ranked
WHERE rn = 1
ORDER BY BucketIndex;";

        var sql = bucketSql is null ? lastTemplate : aggregateTemplate.Replace("{expr}", bucketSql);

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<MetricPointRow>(new CommandDefinition(sql,
            new
            {
                Name = metricName,
                Service = service,
                FromUtc = query.FromUtc,
                ToUtc = query.ToUtc,
                BucketSeconds = bucketSeconds,
            }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<long> DeleteMetricsOlderThanAsync(DateTime olderThanUtc, CancellationToken ct)
    {
        const string sql = "DELETE TOP (@BatchSize) FROM dbo.Metrics WHERE TimestampUtc < @OlderThanUtc;";
        await using var connection = OpenConnection();
        const int batch = 5000;
        long total = 0;
        while (!ct.IsCancellationRequested)
        {
            var deleted = await connection.ExecuteAsync(new CommandDefinition(sql,
                new { OlderThanUtc = olderThanUtc, BatchSize = batch }, cancellationToken: ct));
            total += deleted;
            if (deleted < batch)
                break;
        }
        return total;
    }

    public async Task<ServiceMapResult> QueryServiceMapAsync(DateTime fromUtc, string? service, CancellationToken ct)
    {
        const string nodesSql = @"
SELECT ServiceName,
       MAX(ServiceVersion) AS ServiceVersion,
       MAX(ServiceEnvironment) AS ServiceEnvironment,
       COUNT(DISTINCT TraceId) AS TotalTraces,
       SUM(CASE WHEN StatusCode = N'Error' THEN 1 ELSE 0 END) AS ErrorSpans,
       MAX(StartTimeUtc) AS LastSeenUtc
FROM dbo.Spans
WHERE StartTimeUtc >= @FromUtc
  AND (@Service IS NULL OR ServiceName = @Service)
GROUP BY ServiceName;";

        const string edgesSql = @"
;WITH ParentSvc AS (
    SELECT s.TraceId, s.ServiceName,
           p.ServiceName AS ParentServiceName
    FROM dbo.Spans s
    LEFT JOIN dbo.Spans p ON p.SpanId = s.ParentSpanId AND p.TraceId = s.TraceId
    WHERE s.StartTimeUtc >= @FromUtc
      AND (@Service IS NULL OR s.ServiceName = @Service)
)
SELECT COALESCE(ParentServiceName, N'__root__') AS [Source],
       ServiceName AS [Target],
       COUNT(DISTINCT TraceId) AS CallCount
FROM ParentSvc
GROUP BY COALESCE(ParentServiceName, N'__root__'), ServiceName;";

        await using var connection = OpenConnection();
        var nodes = await connection.QueryAsync<ServiceMapNodeRow>(new CommandDefinition(nodesSql,
            new { FromUtc = fromUtc, Service = service }, cancellationToken: ct));
        var edges = await connection.QueryAsync<ServiceMapEdgeRow>(new CommandDefinition(edgesSql,
            new { FromUtc = fromUtc, Service = service }, cancellationToken: ct));
        return new ServiceMapResult { Nodes = nodes.ToList(), Edges = edges.ToList() };
    }

    public async Task<IReadOnlyList<CollectorRow>> QueryCollectorsAsync(DateTime fromUtc, CancellationToken ct)
    {
        const string sql = @"
SELECT ServiceName,
       MAX(ServiceVersion) AS ServiceVersion,
       MAX(ServiceEnvironment) AS ServiceEnvironment,
       COUNT(DISTINCT TraceId) AS TotalTraces,
       SUM(CASE WHEN StatusCode = N'Error' THEN 1 ELSE 0 END) AS ErrorSpans,
       MAX(StartTimeUtc) AS LastSeenUtc
FROM dbo.Spans
WHERE StartTimeUtc >= @FromUtc
GROUP BY ServiceName
ORDER BY LastSeenUtc DESC;";

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<CollectorRow>(new CommandDefinition(sql,
            new { FromUtc = fromUtc }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<AlertStateRow>> GetAlertStatesAsync(CancellationToken ct)
    {
        const string sql = "SELECT AlertKey, Acknowledged FROM dbo.AlertState WHERE Acknowledged = 1;";
        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<AlertStateRow>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task AcknowledgeAlertAsync(string alertKey, CancellationToken ct)
    {
        const string sql = @"
MERGE dbo.AlertState WITH (HOLDLOCK) AS target
USING (SELECT @AlertKey AS AlertKey) AS source ON target.AlertKey = source.AlertKey
WHEN MATCHED THEN UPDATE SET Acknowledged = 1, AcknowledgedAtUtc = @Now, UpdatedAtUtc = @Now
WHEN NOT MATCHED THEN INSERT (AlertKey, Acknowledged, AcknowledgedAtUtc, UpdatedAtUtc)
    VALUES (@AlertKey, 1, @Now, @Now);";

        await using var connection = OpenConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql,
            new { AlertKey = alertKey, Now = DateTime.UtcNow }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ExpensiveQueryRow>> QueryExpensiveQueriesAsync(
        ExpensiveQueryQuery query, CancellationToken ct)
    {
        const string statementExpr = @"COALESCE(JSON_VALUE(s.AttributesJson, '$.""db.query.text""'),
                     JSON_VALUE(s.AttributesJson, '$.""db.statement""'),
                     JSON_VALUE(s.AttributesJson, '$.""db.query.summary""'))";

        const string sql = @"
;WITH Q AS (
    SELECT s.TraceId, s.ServiceName, s.StartTimeUtc, s.DurationUs,
           " + statementExpr + @" AS Statement,
           JSON_VALUE(s.AttributesJson, '$.""db.query.summary""') AS Summary
    FROM dbo.Spans s
    WHERE s.Kind = N'Client'
      AND s.StartTimeUtc >= @FromUtc
      AND (@ToUtc IS NULL OR s.StartTimeUtc <= @ToUtc)
      AND (@Service IS NULL OR s.ServiceName = @Service)
      AND s.DurationUs >= @MinDurationUs
      AND " + statementExpr + @" IS NOT NULL
      AND " + statementExpr + @" NOT LIKE N'CREATE%'
      AND " + statementExpr + @" NOT LIKE N'ALTER%'
      AND " + statementExpr + @" NOT LIKE N'DROP%'
      AND " + statementExpr + @" NOT LIKE N'TRUNCATE%'
      AND " + statementExpr + @" NOT LIKE N'EXEC%'
      AND " + statementExpr + @" NOT LIKE N'DECLARE%'
),
G AS (
    SELECT Statement,
           MAX(Summary) AS Summary,
           ServiceName,
           COUNT(*) AS Executions,
           AVG(DurationUs) / 1000.0 AS AvgMs,
           MAX(DurationUs) / 1000.0 AS MaxMs,
           SUM(DurationUs) / 1000.0 AS TotalMs,
           MAX(StartTimeUtc) AS LastSeenUtc,
           COUNT(*) OVER () AS Total
    FROM Q
    GROUP BY Statement, ServiceName
)
SELECT g.Statement, g.Summary, g.ServiceName, g.Executions, g.AvgMs, g.MaxMs, g.TotalMs, g.LastSeenUtc,
       (SELECT TOP 1 q2.TraceId FROM Q q2
         WHERE q2.Statement = g.Statement AND q2.ServiceName = g.ServiceName
         ORDER BY q2.StartTimeUtc DESC) AS SampleTraceId,
       g.Total
FROM G g
ORDER BY {orderBy}
OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY;";

        var orderBy = query.Sort?.ToLowerInvariant() switch
        {
            "avg_ms" => "g.AvgMs DESC",
            "executions" => "g.Executions DESC",
            "total_ms" => "g.TotalMs DESC",
            "last_seen" => "g.LastSeenUtc DESC",
            _ => "g.MaxMs DESC",
        };

        var parameters = new DynamicParameters();
        parameters.Add("FromUtc", query.FromUtc ?? DateTime.UtcNow.AddHours(-1), DbType.DateTime2);
        parameters.Add("ToUtc", query.ToUtc, DbType.DateTime2);
        parameters.Add("Service", query.Service);
        parameters.Add("MinDurationUs", Math.Max(0, query.MinDurationUs));
        parameters.Add("Offset", Math.Max(0, query.Offset));
        parameters.Add("Limit", Math.Clamp(query.Limit, 1, 1000));

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<ExpensiveQueryRowSql>(new CommandDefinition(
            sql.Replace("{orderBy}", orderBy), parameters, cancellationToken: ct));

        return rows.Select(r => new ExpensiveQueryRow
        {
            Statement = r.Statement,
            Summary = r.Summary,
            ServiceName = r.ServiceName,
            Executions = r.Executions,
            AvgMs = r.AvgMs,
            MaxMs = r.MaxMs,
            TotalMs = r.TotalMs,
            LastSeenUtc = r.LastSeenUtc,
            SampleTraceId = r.SampleTraceId,
            Total = r.Total,
        }).ToList();
    }

    public async Task<IReadOnlyList<ServiceRow>> QueryServicesAsync(DateTime fromUtc, CancellationToken ct)
    {
        const string sql = @"
;WITH Spans AS (
    SELECT ServiceName,
           MAX(ServiceVersion) AS ServiceVersion,
           MAX(ServiceEnvironment) AS ServiceEnvironment,
           COUNT(DISTINCT TraceId) AS TotalTraces,
           SUM(CASE WHEN StatusCode = N'Error' THEN 1 ELSE 0 END) AS ErrorCount,
           MAX(StartTimeUtc) AS LastSeenUtc
    FROM dbo.Spans
    WHERE StartTimeUtc >= @FromUtc
    GROUP BY ServiceName
),
LogOnly AS (
    SELECT l.ServiceName,
           MAX(l.ServiceVersion) AS ServiceVersion,
           MAX(l.ServiceEnvironment) AS ServiceEnvironment,
           0 AS TotalTraces,
           0 AS ErrorCount,
           MAX(l.TimestampUtc) AS LastSeenUtc
    FROM dbo.Logs l
    WHERE l.TimestampUtc >= @FromUtc
      AND NOT EXISTS (SELECT 1 FROM Spans s WHERE s.ServiceName = l.ServiceName)
    GROUP BY l.ServiceName
)
SELECT ServiceName, ServiceVersion, ServiceEnvironment, TotalTraces, ErrorCount, LastSeenUtc
FROM Spans
UNION ALL
SELECT ServiceName, ServiceVersion, ServiceEnvironment, TotalTraces, ErrorCount, LastSeenUtc
FROM LogOnly
ORDER BY LastSeenUtc DESC;";

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<ServiceRow>(new CommandDefinition(sql,
            new { FromUtc = fromUtc }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<DashboardAggregate> QueryDashboardAggregateAsync(
        DateTime fromUtc, int bucketSeconds, string? service, CancellationToken ct)
    {
        const string totalsSql = @"
;WITH Agg AS (
    SELECT TraceId,
           MAX(CASE WHEN StatusCode = N'Error' THEN 1 ELSE 0 END) AS HasError
    FROM dbo.Spans
    WHERE StartTimeUtc >= @FromUtc
      AND (@Service IS NULL OR EXISTS (SELECT 1 FROM dbo.Spans s2 WHERE s2.TraceId = dbo.Spans.TraceId AND s2.ServiceName = @Service))
    GROUP BY TraceId
),
Totals AS (
    SELECT COUNT(*) AS TotalTraces,
           COALESCE(SUM(CASE WHEN HasError = 1 THEN 1 ELSE 0 END), 0) AS ErrorTraces
    FROM Agg
)
SELECT t.TotalTraces, t.ErrorTraces, s.TotalSpans
FROM Totals t
CROSS JOIN (SELECT COUNT(*) AS TotalSpans
            FROM dbo.Spans
            WHERE StartTimeUtc >= @FromUtc
              AND (@Service IS NULL OR EXISTS (SELECT 1 FROM dbo.Spans s2 WHERE s2.TraceId = dbo.Spans.TraceId AND s2.ServiceName = @Service))) s;";

        const string percentileSql = @"
;WITH Agg AS (
    SELECT DATEDIFF_BIG(MICROSECOND, MIN(StartTimeUtc), MAX(EndTimeUtc)) AS DurationUs
    FROM dbo.Spans
    WHERE StartTimeUtc >= @FromUtc
      AND (@Service IS NULL OR EXISTS (SELECT 1 FROM dbo.Spans s2 WHERE s2.TraceId = dbo.Spans.TraceId AND s2.ServiceName = @Service))
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
      AND (@Service IS NULL OR ServiceName = @Service)
    GROUP BY ServiceName
),
Pct AS (
    SELECT DISTINCT ServiceName,
           PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY DurationUs) OVER (PARTITION BY ServiceName) AS P99Us
    FROM dbo.Spans
    WHERE StartTimeUtc >= @FromUtc
      AND (@Service IS NULL OR ServiceName = @Service)
)
SELECT s.ServiceName, s.ServiceVersion, s.ServiceEnvironment,
       s.TotalTraces, s.TotalSpans, s.ErrorSpans, s.LastSeenUtc, p.P99Us
FROM Stats s
LEFT JOIN Pct p ON p.ServiceName = s.ServiceName
ORDER BY s.LastSeenUtc DESC;";

        await using var connection = OpenConnection();

        var totals = await connection.QueryFirstOrDefaultAsync<TotalsRow>(new CommandDefinition(totalsSql,
            new { FromUtc = fromUtc, Service = service }, cancellationToken: ct));

        var percentile = await connection.QueryFirstOrDefaultAsync<PercentileRow>(new CommandDefinition(percentileSql,
            new { FromUtc = fromUtc, Service = service }, cancellationToken: ct));

        var throughput = await QueryThroughputAsync(fromUtc, bucketSeconds, service, ct);

        var services = await connection.QueryAsync<ServiceStatsSqlRow>(new CommandDefinition(servicesSql,
            new { FromUtc = fromUtc, Service = service }, cancellationToken: ct));

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
        DateTime fromUtc, int bucketSeconds, string? service, CancellationToken ct)
    {
        const string sql = @"
;WITH Spans AS (
    SELECT s.TraceId,
           DATEDIFF(SECOND, @FromUtc, s.StartTimeUtc) / @BucketSeconds AS BucketIndex
    FROM dbo.Spans s
    WHERE s.StartTimeUtc >= @FromUtc
      AND (@Service IS NULL OR EXISTS (SELECT 1 FROM dbo.Spans s2 WHERE s2.TraceId = s.TraceId AND s2.ServiceName = @Service))
)
SELECT BucketIndex, COUNT(DISTINCT TraceId) AS [Count]
FROM Spans
GROUP BY BucketIndex
ORDER BY BucketIndex;";

        await using var connection = OpenConnection();
        var rows = await connection.QueryAsync<ThroughputBucketRow>(new CommandDefinition(sql,
            new { FromUtc = fromUtc, BucketSeconds = Math.Max(1, bucketSeconds), Service = service }, cancellationToken: ct));
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

    private sealed class LogRowSql
    {
        public required DateTime TimestampUtc { get; init; }
        public required string ServiceName { get; init; }
        public string? ServiceVersion { get; init; }
        public string? ServiceEnvironment { get; init; }
        public required string SeverityText { get; init; }
        public int SeverityNumber { get; init; }
        public string? Body { get; init; }
        public string? TraceId { get; init; }
        public string? SpanId { get; init; }
        public string? AttributesJson { get; init; }
        public required int Total { get; init; }
    }

    private sealed class ExpensiveQueryRowSql
    {
        public required string Statement { get; init; }
        public string? Summary { get; init; }
        public required string ServiceName { get; init; }
        public required long Executions { get; init; }
        public required double AvgMs { get; init; }
        public required double MaxMs { get; init; }
        public required double TotalMs { get; init; }
        public required DateTime LastSeenUtc { get; init; }
        public string? SampleTraceId { get; init; }
        public required int Total { get; init; }
    }
}
