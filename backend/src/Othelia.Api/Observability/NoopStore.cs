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

    public Task<IReadOnlyList<SpanRecord>> QuerySpansAsync(string traceId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SpanRecord>>(Array.Empty<SpanRecord>());

    public Task<IReadOnlyList<ServiceRow>> QueryServicesAsync(DateTime fromUtc, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ServiceRow>>(Array.Empty<ServiceRow>());

    public Task<DashboardAggregate> QueryDashboardAggregateAsync(
        DateTime fromUtc, int bucketSeconds, CancellationToken ct)
        => Task.FromResult(new DashboardAggregate
        {
            TotalTraces = 0,
            TotalSpans = 0,
            ErrorTraces = 0,
            Throughput = Array.Empty<ThroughputBucketRow>(),
            ServiceStats = Array.Empty<ServiceStatsRow>(),
        });

    public Task<IReadOnlyList<ThroughputBucketRow>> QueryThroughputAsync(
        DateTime fromUtc, int bucketSeconds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ThroughputBucketRow>>(Array.Empty<ThroughputBucketRow>());

    public Task<long> DeleteSpansOlderThanAsync(DateTime olderThanUtc, CancellationToken ct) => Task.FromResult(0L);
}
