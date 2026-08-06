namespace Othelia.Api.Models;

public sealed record SpanDto
{
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public required string TraceId { get; init; }
    public required string Name { get; init; }
    public required string ServiceName { get; init; }
    public required string Kind { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public TimeSpan Duration { get; init; }
    public string? Status { get; init; }
    public IReadOnlyDictionary<string, string>? Attributes { get; init; }
    public IReadOnlyDictionary<string, string>? ResourceAttributes { get; init; }
    public IReadOnlyList<SpanEventDto>? Events { get; init; }
    public IReadOnlyList<SpanLinkDto>? Links { get; init; }
}

public sealed record SpanLinkDto
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public IReadOnlyDictionary<string, string>? Attributes { get; init; }
}
