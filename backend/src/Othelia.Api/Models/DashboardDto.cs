namespace Othelia.Api.Models;

public sealed record DashboardDto
{
    public required DateTimeOffset GeneratedAt { get; init; }
    public required long WindowSeconds { get; init; }
    public required int Buckets { get; init; }
    public required DashboardMetricsDto Metrics { get; init; }
    public required IReadOnlyList<ThroughputPointDto> Throughput { get; init; }
    public required IReadOnlyList<ServiceHealthDto> Services { get; init; }
    public required IReadOnlyList<AlertDto> Alerts { get; init; }
}

public sealed record DashboardMetricsDto
{
    public required long TotalRequests { get; init; }
    public required long TotalSpans { get; init; }
    public required double ErrorRate { get; init; }
    public required double P50Ms { get; init; }
    public required double P99Ms { get; init; }
    public required double ThroughputRps { get; init; }
    public required int ServicesTotal { get; init; }
    public required int ServicesUp { get; init; }
    public required int ServicesDown { get; init; }
}

public sealed record ThroughputPointDto
{
    public required string Time { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required double Value { get; init; }
}

public sealed record ThroughputSeriesDto
{
    public required string Range { get; init; }
    public required int WindowSeconds { get; init; }
    public required int BucketSeconds { get; init; }
    public required IReadOnlyList<ThroughputPointDto> Points { get; init; }
}

public sealed record ServiceHealthDto
{
    public required string Name { get; init; }
    public string? Version { get; init; }
    public string? Environment { get; init; }
    public required string Status { get; init; }
    public required double Rps { get; init; }
    public required double ErrorRate { get; init; }
    public required double P99Ms { get; init; }
    public required double Uptime { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
    public required long TotalTraces { get; init; }
}

public sealed record AlertDto
{
    public required string Id { get; init; }
    public required string Level { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string Service { get; init; }
    public required DateTimeOffset Time { get; init; }
    public required bool Acknowledged { get; init; }
    public string? TraceId { get; init; }
}
