namespace Othelia.Api.Models;

public sealed record TraceSummaryDto
{
    public required string TraceId { get; init; }
    public required string Name { get; init; }
    public required string RootServiceName { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public TimeSpan Duration { get; init; }
    public int SpanCount { get; init; }
    public string? Status { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
}
