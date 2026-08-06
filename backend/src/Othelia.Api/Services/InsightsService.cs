using Othelia.Api.Models;
using Othelia.Api.Observability;

namespace Othelia.Api.Services;

public interface IInsightsService
{
    Task<IReadOnlyList<MetricNameDto>> GetMetricNamesAsync(string? service, CancellationToken ct = default);
    Task<MetricSeriesDto> GetMetricSeriesAsync(string metric, string? service, string? aggregation, int? bucketSeconds, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<ServiceMapDto> GetServiceMapAsync(string? service, CancellationToken ct = default);
    Task<IReadOnlyList<CollectorDto>> GetCollectorsAsync(CancellationToken ct = default);
}

public sealed class InsightsService : IInsightsService
{
    private readonly ITelemetryStore _store;
    private readonly ITracingOptionsResolver _resolver;

    public InsightsService(ITelemetryStore store, ITracingOptionsResolver resolver)
    {
        _store = store;
        _resolver = resolver;
    }

    public async Task<IReadOnlyList<MetricNameDto>> GetMetricNamesAsync(string? service, CancellationToken ct = default)
    {
        var opts = await _resolver.ResolveAsync(ct);
        var fromUtc = DateTime.UtcNow.AddSeconds(-Math.Max(0, opts.Query.DefaultLookbackSeconds));
        var rows = await _store.QueryMetricNamesAsync(fromUtc, Normalize(service), ct);

        return rows.Select(r => new MetricNameDto
        {
            Name = r.Name,
            Unit = r.Unit,
            DataPoints = r.DataPoints,
            LastSeen = new DateTimeOffset(r.LastSeenUtc, TimeSpan.Zero),
        }).ToList();
    }

    public async Task<MetricSeriesDto> GetMetricSeriesAsync(
        string metric, string? service, string? aggregation, int? bucketSeconds, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var opts = await _resolver.ResolveAsync(ct);
        var effectiveAggregation = string.IsNullOrWhiteSpace(aggregation) ? "avg" : aggregation.Trim().ToLowerInvariant();
        if (effectiveAggregation is not ("avg" or "min" or "max" or "sum" or "last"))
            effectiveAggregation = "avg";

        var fromUtc = from ?? DateTime.UtcNow.AddSeconds(-Math.Max(0, opts.Query.DefaultLookbackSeconds));
        var query = new MetricQuery
        {
            Service = Normalize(service),
            FromUtc = fromUtc,
            ToUtc = to,
            BucketSeconds = Math.Max(1, bucketSeconds ?? opts.Metrics.DefaultBucketSeconds),
            Aggregation = effectiveAggregation,
        };

        var rows = await _store.QueryMetricPointsAsync(metric.Trim(), query.Service, query, ct);

        var useSeconds = (query.ToUtc ?? DateTime.UtcNow) - fromUtc < TimeSpan.FromHours(1);
        var points = rows.Select(r => new MetricPointDto
        {
            Time = useSeconds ? r.TimestampUtc.ToString("HH:mm:ss") : r.TimestampUtc.ToString("HH:mm"),
            Timestamp = new DateTimeOffset(r.TimestampUtc, TimeSpan.Zero),
            Value = r.Value,
        }).ToList();

        return new MetricSeriesDto
        {
            Metric = metric.Trim(),
            Service = query.Service,
            Aggregation = effectiveAggregation,
            BucketSeconds = query.BucketSeconds,
            Points = points,
        };
    }

    public async Task<ServiceMapDto> GetServiceMapAsync(string? service, CancellationToken ct = default)
    {
        var opts = await _resolver.ResolveAsync(ct);
        var fromUtc = DateTime.UtcNow.AddSeconds(-Math.Max(1, opts.ServiceMap.WindowSeconds));
        var result = await _store.QueryServiceMapAsync(fromUtc, Normalize(service), ct);

        var nodes = result.Nodes.Select(n => new ServiceMapNodeDto
        {
            Name = n.ServiceName,
            Version = n.ServiceVersion,
            Environment = n.ServiceEnvironment,
            TotalTraces = n.TotalTraces,
            ErrorSpans = n.ErrorSpans,
            LastSeen = new DateTimeOffset(n.LastSeenUtc, TimeSpan.Zero),
        }).ToList();

        var edges = result.Edges
            .Where(e => e.Source != "__root__" && nodes.Any(n => n.Name == e.Source) && nodes.Any(n => n.Name == e.Target))
            .Select(e => new ServiceMapEdgeDto { Source = e.Source, Target = e.Target, CallCount = e.CallCount })
            .ToList();

        return new ServiceMapDto { Nodes = nodes, Edges = edges };
    }

    public async Task<IReadOnlyList<CollectorDto>> GetCollectorsAsync(CancellationToken ct = default)
    {
        var opts = await _resolver.ResolveAsync(ct);
        var fromUtc = DateTime.UtcNow.AddSeconds(-Math.Max(1, opts.Collectors.WindowSeconds));
        var staleCutoff = DateTime.UtcNow.AddMinutes(-Math.Max(1, opts.Collectors.StaleMinutes));
        var rows = await _store.QueryCollectorsAsync(fromUtc, ct);

        return rows.Select(r => new CollectorDto
        {
            Name = r.ServiceName,
            Version = r.ServiceVersion,
            Environment = r.ServiceEnvironment,
            Running = r.LastSeenUtc >= staleCutoff,
            TotalTraces = r.TotalTraces,
            ErrorSpans = r.ErrorSpans,
            LastSeen = new DateTimeOffset(r.LastSeenUtc, TimeSpan.Zero),
        }).ToList();
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
