using Microsoft.Extensions.Options;
using Othelia.Api.Models;
using Othelia.Api.Observability;

namespace Othelia.Api.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
    Task<ThroughputSeriesDto> GetThroughputAsync(string? range, CancellationToken ct = default);
}

public sealed class DashboardService : IDashboardService
{
    private const int MaxAlerts = 10;
    private const double ErrorRateDownThreshold = 5.0;

    private readonly ITelemetryStore _store;
    private readonly TracingOptions _options;

    public DashboardService(ITelemetryStore store, IOptions<TracingOptions> options)
    {
        _store = store;
        _options = options.Value;
    }

    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var query = _options.Query;
        var windowSeconds = Math.Max(60, query.DashboardWindowSeconds);
        var buckets = Math.Max(1, query.DashboardBuckets);
        var bucketSeconds = Math.Max(60, windowSeconds / buckets);

        var nowUtc = DateTime.UtcNow;
        var fromUtc = nowUtc.AddSeconds(-windowSeconds);

        var aggregate = await _store.QueryDashboardAggregateAsync(fromUtc, bucketSeconds, ct);

        var alerts = await BuildAlertsAsync(fromUtc, query.SlowThresholdMs, ct);

        var metrics = BuildMetrics(aggregate, windowSeconds);
        var throughput = BuildThroughput(aggregate, fromUtc, bucketSeconds, buckets);
        var services = BuildServices(aggregate, windowSeconds, query.SlowThresholdMs);

        return new DashboardDto
        {
            GeneratedAt = new DateTimeOffset(nowUtc, TimeSpan.Zero),
            WindowSeconds = windowSeconds,
            Buckets = buckets,
            Metrics = metrics,
            Throughput = throughput,
            Services = services,
            Alerts = alerts,
        };
    }

    private DashboardMetricsDto BuildMetrics(DashboardAggregate aggregate, int windowSeconds)
    {
        var totalTraces = aggregate.TotalTraces;
        var errorRate = totalTraces > 0 ? aggregate.ErrorTraces / (double)totalTraces * 100.0 : 0.0;

        var stats = aggregate.ServiceStats;
        var services = BuildServices(aggregate, windowSeconds, _options.Query.SlowThresholdMs);
        var servicesDown = services.Count(s => s.Status == "error");
        var servicesUp = services.Count - servicesDown;

        return new DashboardMetricsDto
        {
            TotalRequests = totalTraces,
            TotalSpans = aggregate.TotalSpans,
            ErrorRate = Math.Round(errorRate, 2),
            P50Ms = RoundMs(aggregate.P50Us),
            P99Ms = RoundMs(aggregate.P99Us),
            ThroughputRps = Math.Round(totalTraces / (double)windowSeconds, 2),
            ServicesTotal = stats.Count,
            ServicesUp = servicesUp,
            ServicesDown = servicesDown,
        };
    }

    private static IReadOnlyList<ThroughputPointDto> BuildThroughput(
        DashboardAggregate aggregate, DateTime fromUtc, int bucketSeconds, int buckets)
    {
        var points = new List<ThroughputPointDto>(buckets);
        for (var i = 0; i < buckets; i++)
        {
            var count = aggregate.Throughput.FirstOrDefault(b => b.BucketIndex == i)?.Count ?? 0;
            var bucketStart = fromUtc.AddSeconds(i * bucketSeconds);
            points.Add(new ThroughputPointDto
            {
                Time = bucketStart.ToString("HH:mm"),
                Timestamp = new DateTimeOffset(bucketStart, TimeSpan.Zero),
                Value = count,
            });
        }
        return points;
    }

    private IReadOnlyList<ServiceHealthDto> BuildServices(
        DashboardAggregate aggregate, int windowSeconds, int slowThresholdMs)
    {
        var services = new List<ServiceHealthDto>();
        foreach (var s in aggregate.ServiceStats)
        {
            var errorRate = s.TotalSpans > 0 ? s.ErrorSpans / (double)s.TotalSpans * 100.0 : 0.0;
            var p99Ms = s.P99Us.HasValue ? s.P99Us.Value / 1000.0 : 0.0;
            var status = errorRate >= ErrorRateDownThreshold
                ? "error"
                : p99Ms >= slowThresholdMs
                    ? "slow"
                    : "ok";

            services.Add(new ServiceHealthDto
            {
                Name = s.ServiceName,
                Version = s.ServiceVersion,
                Environment = s.ServiceEnvironment,
                Status = status,
                Rps = Math.Round(s.TotalTraces / (double)windowSeconds, 2),
                ErrorRate = Math.Round(errorRate, 2),
                P99Ms = RoundMs(s.P99Us),
                Uptime = Math.Round(Math.Clamp(100.0 - errorRate, 0.0, 100.0), 2),
                LastSeen = new DateTimeOffset(s.LastSeenUtc, TimeSpan.Zero),
                TotalTraces = s.TotalTraces,
            });
        }

        return services
            .OrderBy(s => s.Status == "error" ? 0 : s.Status == "slow" ? 1 : 2)
            .ThenByDescending(s => s.LastSeen)
            .ToList();
    }

    private async Task<IReadOnlyList<AlertDto>> BuildAlertsAsync(DateTime fromUtc, int slowThresholdMs, CancellationToken ct)
    {
        var errorTraces = await _store.QueryRecentTracesAsync(new TraceQuery
        {
            FromUtc = fromUtc,
            Status = "error",
            Limit = 6,
            Sort = "start_desc",
        }, ct);

        var slowTraces = await _store.QueryRecentTracesAsync(new TraceQuery
        {
            FromUtc = fromUtc,
            Status = "slow",
            SlowThresholdMs = slowThresholdMs,
            Limit = 6,
            Sort = "start_desc",
        }, ct);

        var alerts = new List<AlertDto>(errorTraces.Count + slowTraces.Count);

        foreach (var t in errorTraces)
        {
            var name = string.IsNullOrEmpty(t.RootName) ? "trace" : t.RootName;
            alerts.Add(new AlertDto
            {
                Id = $"err-{t.TraceId}",
                Level = "error",
                Title = $"Error in {name}",
                Message = $"{t.RootService} finished in error ({t.SpanCount} spans, {FormatDuration(t.DurationUs / 1000.0)})",
                Service = string.IsNullOrEmpty(t.RootService) ? "unknown" : t.RootService,
                Time = new DateTimeOffset(t.StartTimeUtc, TimeSpan.Zero),
                Acknowledged = false,
            });
        }

        foreach (var t in slowTraces)
        {
            var name = string.IsNullOrEmpty(t.RootName) ? "trace" : t.RootName;
            alerts.Add(new AlertDto
            {
                Id = $"slow-{t.TraceId}",
                Level = "warning",
                Title = $"Slow trace: {name}",
                Message = $"{t.RootService} took {FormatDuration(t.DurationUs / 1000.0)} (threshold {slowThresholdMs}ms)",
                Service = string.IsNullOrEmpty(t.RootService) ? "unknown" : t.RootService,
                Time = new DateTimeOffset(t.StartTimeUtc, TimeSpan.Zero),
                Acknowledged = false,
            });
        }

        return alerts
            .OrderByDescending(a => a.Time)
            .Take(MaxAlerts)
            .ToList();
    }

    private static double RoundMs(double? durationUs)
        => durationUs.HasValue ? Math.Round(durationUs.Value / 1000.0, 2) : 0.0;

    public async Task<ThroughputSeriesDto> GetThroughputAsync(string? range, CancellationToken ct = default)
    {
        var (windowSeconds, bucketSeconds) = ResolveRange(range);
        var fromUtc = DateTime.UtcNow.AddSeconds(-windowSeconds);
        var buckets = await _store.QueryThroughputAsync(fromUtc, bucketSeconds, ct);

        var count = (int)Math.Ceiling(windowSeconds / (double)bucketSeconds);
        var points = new List<ThroughputPointDto>(count);
        var useSeconds = windowSeconds < 3600;

        for (var i = 0; i < count; i++)
        {
            var bucketCount = buckets.FirstOrDefault(b => b.BucketIndex == i)?.Count ?? 0;
            var bucketStart = fromUtc.AddSeconds(i * bucketSeconds);
            points.Add(new ThroughputPointDto
            {
                Time = useSeconds ? bucketStart.ToString("HH:mm:ss") : bucketStart.ToString("HH:mm"),
                Timestamp = new DateTimeOffset(bucketStart, TimeSpan.Zero),
                Value = bucketCount,
            });
        }

        return new ThroughputSeriesDto
        {
            Range = range ?? "24h",
            WindowSeconds = windowSeconds,
            BucketSeconds = bucketSeconds,
            Points = points,
        };
    }

    private static (int windowSeconds, int bucketSeconds) ResolveRange(string? range)
        => range?.Trim().ToLowerInvariant() switch
        {
            "1m" => (60, 1),
            "5m" => (300, 5),
            "15m" => (900, 15),
            "30m" => (1800, 30),
            "1h" => (3600, 60),
            _ => (86400, 3600),
        };

    private static string FormatDuration(double ms)
    {
        if (ms < 1000) return $"{Math.Round(ms)}ms";
        if (ms < 60_000) return $"{Math.Round(ms / 1000.0, 2)}s";
        return $"{Math.Round(ms / 60_000.0, 1)}min";
    }
}
