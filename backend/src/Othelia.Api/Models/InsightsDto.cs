namespace Othelia.Api.Models;

public sealed record MetricNameDto
{
    public required string Name { get; init; }
    public string? Unit { get; init; }
    public required long DataPoints { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
}

public sealed record MetricPointDto
{
    public required string Time { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required double Value { get; init; }
}

public sealed record MetricSeriesDto
{
    public required string Metric { get; init; }
    public string? Service { get; init; }
    public string Aggregation { get; init; } = "avg";
    public required int BucketSeconds { get; init; }
    public required IReadOnlyList<MetricPointDto> Points { get; init; }
}

public sealed record ServiceMapNodeDto
{
    public required string Name { get; init; }
    public string? Version { get; init; }
    public string? Environment { get; init; }
    public required long TotalTraces { get; init; }
    public required long ErrorSpans { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
}

public sealed record ServiceMapEdgeDto
{
    public required string Source { get; init; }
    public required string Target { get; init; }
    public required long CallCount { get; init; }
}

public sealed record ServiceMapDto
{
    public required IReadOnlyList<ServiceMapNodeDto> Nodes { get; init; }
    public required IReadOnlyList<ServiceMapEdgeDto> Edges { get; init; }
}

public sealed record CollectorDto
{
    public required string Name { get; init; }
    public string? Version { get; init; }
    public string? Environment { get; init; }
    public required bool Running { get; init; }
    public required long TotalTraces { get; init; }
    public required long ErrorSpans { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
}
