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
