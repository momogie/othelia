using Othelia.Api.Models;

namespace Othelia.Api.Observability;

public sealed record TraceQuery
{
    public string? Service { get; init; }
    public string? TraceId { get; init; }
    public string? Status { get; init; }
    public string? Name { get; init; }
    public string? NameNot { get; init; }
    public string? Path { get; init; }
    public string? PathNot { get; init; }
    public string? Attributes { get; init; }
    public long? MinDurationMs { get; init; }
    public long? MaxDurationMs { get; init; }
    public int? SlowThresholdMs { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int Limit { get; init; } = 100;
    public int Offset { get; init; }
    public string? Sort { get; init; }
}

public sealed record TraceQueryResult
{
    public required IReadOnlyList<TraceSummaryDto> Items { get; init; }
    public required int Total { get; init; }
}
