namespace Othelia.Api.Models;

public sealed record ServiceDto
{
    public required string Name { get; init; }
    public string? Version { get; init; }
    public string? Environment { get; init; }
    public DateTimeOffset LastSeen { get; init; }
    public long TotalTraces { get; init; }
    public long ErrorCount { get; init; }
}
