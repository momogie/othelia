namespace Othelia.Api.Observability;

public sealed class NoopSchemaInitializer : ISchemaInitializer
{
    public Task EnsureSchemaAsync(CancellationToken ct) => Task.CompletedTask;
}

public sealed class NoopTelemetryStore : ITelemetryStore
{
    public Task InsertSpansAsync(IReadOnlyList<SpanRecord> spans, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<TraceRow>> QueryRecentTracesAsync(TraceQuery query, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<TraceRow>>(Array.Empty<TraceRow>());

    public Task<IReadOnlyList<SpanRecord>> QuerySpansAsync(string traceId, int maxRows, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SpanRecord>>(Array.Empty<SpanRecord>());

    public Task<IReadOnlyList<ServiceRow>> QueryServicesAsync(DateTime fromUtc, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ServiceRow>>(Array.Empty<ServiceRow>());

    public Task<DashboardAggregate> QueryDashboardAggregateAsync(
        DateTime fromUtc, int bucketSeconds, string? service, CancellationToken ct)
        => Task.FromResult(new DashboardAggregate
        {
            TotalTraces = 0,
            TotalSpans = 0,
            ErrorTraces = 0,
            Throughput = Array.Empty<ThroughputBucketRow>(),
            ServiceStats = Array.Empty<ServiceStatsRow>(),
        });

    public Task<IReadOnlyList<ThroughputBucketRow>> QueryThroughputAsync(
        DateTime fromUtc, int bucketSeconds, string? service, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ThroughputBucketRow>>(Array.Empty<ThroughputBucketRow>());

    public Task<long> DeleteSpansOlderThanAsync(DateTime olderThanUtc, CancellationToken ct) => Task.FromResult(0L);

    public Task InsertLogsAsync(IReadOnlyList<LogRecord> logs, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery query, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<LogRow>>(Array.Empty<LogRow>());

    public Task<long> DeleteLogsOlderThanAsync(DateTime olderThanUtc, CancellationToken ct) => Task.FromResult(0L);

    public Task<bool> IsHealthyAsync(CancellationToken ct) => Task.FromResult(true);

    public Task InsertMetricsAsync(IReadOnlyList<MetricRecord> metrics, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<MetricNameRow>> QueryMetricNamesAsync(DateTime fromUtc, string? service, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MetricNameRow>>(Array.Empty<MetricNameRow>());

    public Task<IReadOnlyList<MetricPointRow>> QueryMetricPointsAsync(string metricName, string? service, MetricQuery query, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MetricPointRow>>(Array.Empty<MetricPointRow>());

    public Task<long> DeleteMetricsOlderThanAsync(DateTime olderThanUtc, CancellationToken ct) => Task.FromResult(0L);

    public Task<ServiceMapResult> QueryServiceMapAsync(DateTime fromUtc, string? service, CancellationToken ct)
        => Task.FromResult(new ServiceMapResult { Nodes = Array.Empty<ServiceMapNodeRow>(), Edges = Array.Empty<ServiceMapEdgeRow>() });

    public Task<IReadOnlyList<CollectorRow>> QueryCollectorsAsync(DateTime fromUtc, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CollectorRow>>(Array.Empty<CollectorRow>());

    public Task<IReadOnlyList<AlertStateRow>> GetAlertStatesAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<AlertStateRow>>(Array.Empty<AlertStateRow>());

    public Task AcknowledgeAlertAsync(string alertKey, CancellationToken ct) => Task.CompletedTask;
}
