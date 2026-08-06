namespace Othelia.Api.Observability;

public sealed record SpanRecord
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public required string Name { get; init; }
    public required string ServiceName { get; init; }
    public required string Kind { get; init; }
    public required DateTime StartTimeUtc { get; init; }
    public required DateTime EndTimeUtc { get; init; }
    public required long DurationUs { get; init; }
    public required string StatusCode { get; init; }
    public string? StatusMessage { get; init; }
    public string? HttpMethod { get; init; }
    public string? HttpPath { get; init; }
    public int? HttpStatusCode { get; init; }
    public string? ServiceVersion { get; init; }
    public string? ServiceEnvironment { get; init; }
    public string? ResourceJson { get; init; }
    public string? AttributesJson { get; init; }
    public string? EventsJson { get; init; }
    public string? LinksJson { get; init; }
}

public sealed record TraceRow
{
    public required string TraceId { get; init; }
    public required DateTime StartTimeUtc { get; init; }
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

public sealed record ServiceRow
{
    public required string ServiceName { get; init; }
    public string? ServiceVersion { get; init; }
    public string? ServiceEnvironment { get; init; }
    public required long TotalTraces { get; init; }
    public required long ErrorCount { get; init; }
    public required DateTime LastSeenUtc { get; init; }
}

public sealed record DashboardAggregate
{
    public required long TotalTraces { get; init; }
    public required long TotalSpans { get; init; }
    public required long ErrorTraces { get; init; }
    public double? P50Us { get; init; }
    public double? P99Us { get; init; }
    public required IReadOnlyList<ThroughputBucketRow> Throughput { get; init; }
    public required IReadOnlyList<ServiceStatsRow> ServiceStats { get; init; }
}

public sealed record ThroughputBucketRow
{
    public required int BucketIndex { get; init; }
    public required long Count { get; init; }
}

public sealed record ServiceStatsRow
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

public sealed record LogRecord
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
    public string? ResourceJson { get; init; }
}

public sealed record LogRow
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

public sealed record LogQuery
{
    public string? Service { get; init; }
    public string? Severity { get; init; }
    public string? Search { get; init; }
    public string? TraceId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int Limit { get; init; } = 100;
    public int Offset { get; init; }
    public string? Sort { get; init; }
}
