namespace Othelia.Api.Models;

public sealed record ExpensiveQueryDto
{
    public required string Statement { get; init; }
    public string? Summary { get; init; }
    public required string ServiceName { get; init; }
    public required long Executions { get; init; }
    public required double AvgMs { get; init; }
    public required double MaxMs { get; init; }
    public required double TotalMs { get; init; }
    public DateTimeOffset LastSeen { get; init; }
    public string? SampleTraceId { get; init; }
    public string? Status { get; init; }
}

public sealed record ExpensiveQueryResult
{
    public required IReadOnlyList<ExpensiveQueryDto> Items { get; init; }
    public required int Total { get; init; }
}
