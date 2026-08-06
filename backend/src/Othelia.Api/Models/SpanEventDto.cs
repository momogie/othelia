namespace Othelia.Api.Models;

public sealed record SpanEventDto
{
    public required string Name { get; init; }
    public DateTimeOffset Time { get; init; }
    public IReadOnlyDictionary<string, string>? Attributes { get; init; }
}
