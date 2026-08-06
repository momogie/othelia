namespace Othelia.Api.Observability;

public sealed class NoopSchemaInitializer : ISchemaInitializer
{
    public Task EnsureSchemaAsync(CancellationToken ct) => Task.CompletedTask;
}

public sealed class NoopTelemetryStore : ITelemetryStore
{
    public Task InsertSpansAsync(IReadOnlyList<SpanRecord> spans, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<TraceRow>> QueryRecentTracesAsync(
        string? service, string? traceId, DateTime fromUtc, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<TraceRow>>(Array.Empty<TraceRow>());

    public Task<IReadOnlyList<SpanRecord>> QuerySpansAsync(string traceId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SpanRecord>>(Array.Empty<SpanRecord>());

    public Task<IReadOnlyList<ServiceRow>> QueryServicesAsync(DateTime fromUtc, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ServiceRow>>(Array.Empty<ServiceRow>());
}
