namespace Othelia.Api.Models;

public sealed record LogDto
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string ServiceName { get; init; }
    public string? ServiceVersion { get; init; }
    public string? ServiceEnvironment { get; init; }
    public required string Severity { get; init; }
    public string? Body { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public IReadOnlyDictionary<string, string>? Attributes { get; init; }
}

public sealed record LogQueryResult
{
    public required IReadOnlyList<LogDto> Items { get; init; }
    public required int Total { get; init; }
}
