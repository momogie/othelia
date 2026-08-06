using Othelia.Api.Models;
using Othelia.Api.Observability;

namespace Othelia.Api.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(string? service, CancellationToken ct = default);
    Task<ThroughputSeriesDto> GetThroughputAsync(string? range, string? service, CancellationToken ct = default);
    Task<IReadOnlyList<AlertDto>> GetAlertsAsync(string? service, CancellationToken ct = default);
    Task AcknowledgeAlertAsync(string alertKey, CancellationToken ct = default);
}

public sealed class DashboardService : IDashboardService
{
    private readonly ITelemetryStore _store;
    private readonly ITracingOptionsResolver _resolver;

    public DashboardService(ITelemetryStore store, ITracingOptionsResolver resolver)
    {
        _store = store;
        _resolver = resolver;
    }

    public async Task<DashboardDto> GetAsync(string? service, CancellationToken ct = default)
    {
        var opts = await _resolver.ResolveAsync(ct);
        var query = opts.Query;
        var windowSeconds = Math.Max(60, query.DashboardWindowSeconds);
        var buckets = Math.Max(1, query.DashboardBuckets);
        var bucketSeconds = Math.Max(60, windowSeconds / buckets);

        var nowUtc = DateTime.UtcNow;
        var fromUtc = nowUtc.AddSeconds(-windowSeconds);

        var aggregate = await _store.QueryDashboardAggregateAsync(fromUtc, bucketSeconds, service, ct);

        var alerts = await BuildAlertsAsync(fromUtc, Math.Max(1, query.SlowThresholdMs), opts.Alerts, service, ct);

        var metrics = BuildMetrics(aggregate, windowSeconds, Math.Max(1, query.SlowThresholdMs), opts.Alerts.ErrorRateDownThreshold);
        var throughput = BuildThroughput(aggregate, fromUtc, bucketSeconds, buckets);
        var services = BuildServices(aggregate, windowSeconds, Math.Max(1, query.SlowThresholdMs), opts.Alerts.ErrorRateDownThreshold);

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

    private DashboardMetricsDto BuildMetrics(
        DashboardAggregate aggregate, int windowSeconds, int slowThresholdMs, double errorRateDownThreshold)
    {
        var totalTraces = aggregate.TotalTraces;
        var errorRate = totalTraces > 0 ? aggregate.ErrorTraces / (double)totalTraces * 100.0 : 0.0;

        var stats = aggregate.ServiceStats;
        var services = BuildServices(aggregate, windowSeconds, slowThresholdMs, errorRateDownThreshold);
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
        DashboardAggregate aggregate, int windowSeconds, int slowThresholdMs, double errorRateDownThreshold)
    {
        var services = new List<ServiceHealthDto>();
        foreach (var s in aggregate.ServiceStats)
        {
            var errorRate = s.TotalSpans > 0 ? s.ErrorSpans / (double)s.TotalSpans * 100.0 : 0.0;
            var p99Ms = s.P99Us.HasValue ? s.P99Us.Value / 1000.0 : 0.0;
            var status = errorRate >= errorRateDownThreshold
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

    private async Task<IReadOnlyList<AlertDto>> BuildAlertsAsync(
        DateTime fromUtc, int slowThresholdMs, TracingAlertsOptions alerts, string? service, CancellationToken ct)
    {
        var errorTraces = await _store.QueryRecentTracesAsync(new TraceQuery
        {
            FromUtc = fromUtc,
            Status = "error",
            Service = service,
            Limit = Math.Max(1, alerts.MaxAlerts),
            Sort = "start_desc",
        }, ct);

        var slowTraces = await _store.QueryRecentTracesAsync(new TraceQuery
        {
            FromUtc = fromUtc,
            Status = "slow",
            Service = service,
            SlowThresholdMs = slowThresholdMs,
            Limit = Math.Max(1, alerts.MaxAlerts),
            Sort = "start_desc",
        }, ct);

        var ackState = (await _store.GetAlertStatesAsync(ct)).ToDictionary(a => a.AlertKey, a => a.Acknowledged);

        var alerts2 = new List<AlertDto>(errorTraces.Count + slowTraces.Count);

        foreach (var t in errorTraces)
        {
            var name = string.IsNullOrEmpty(t.RootName) ? "trace" : t.RootName;
            var key = $"err-{t.TraceId}";
            alerts2.Add(new AlertDto
            {
                Id = key,
                Level = "error",
                Title = $"Error in {name}",
                Message = $"{t.RootService} finished in error ({t.SpanCount} spans, {FormatDuration(t.DurationUs / 1000.0)})",
                Service = string.IsNullOrEmpty(t.RootService) ? "unknown" : t.RootService,
                Time = new DateTimeOffset(t.StartTimeUtc, TimeSpan.Zero),
                Acknowledged = ackState.TryGetValue(key, out var ack) && ack,
            });
        }

        foreach (var t in slowTraces)
        {
            var name = string.IsNullOrEmpty(t.RootName) ? "trace" : t.RootName;
            var key = $"slow-{t.TraceId}";
            alerts2.Add(new AlertDto
            {
                Id = key,
                Level = "warning",
                Title = $"Slow trace: {name}",
                Message = $"{t.RootService} took {FormatDuration(t.DurationUs / 1000.0)} (threshold {slowThresholdMs}ms)",
                Service = string.IsNullOrEmpty(t.RootService) ? "unknown" : t.RootService,
                Time = new DateTimeOffset(t.StartTimeUtc, TimeSpan.Zero),
                Acknowledged = ackState.TryGetValue(key, out var ack) && ack,
            });
        }

        return alerts2
            .OrderByDescending(a => a.Time)
            .Take(Math.Max(1, alerts.MaxAlerts))
            .ToList();
    }

    public async Task<IReadOnlyList<AlertDto>> GetAlertsAsync(string? service, CancellationToken ct = default)
    {
        var opts = await _resolver.ResolveAsync(ct);
        if (!opts.Alerts.Enabled)
            return Array.Empty<AlertDto>();

        var windowSeconds = Math.Max(60, opts.Query.DashboardWindowSeconds);
        var fromUtc = DateTime.UtcNow.AddSeconds(-windowSeconds);
        return await BuildAlertsAsync(fromUtc, Math.Max(1, opts.Query.SlowThresholdMs), opts.Alerts, service, ct);
    }

    public async Task AcknowledgeAlertAsync(string alertKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(alertKey))
            return;
        await _store.AcknowledgeAlertAsync(alertKey.Trim(), ct);
    }

    private static double RoundMs(double? durationUs)
        => durationUs.HasValue ? Math.Round(durationUs.Value / 1000.0, 2) : 0.0;

    public async Task<ThroughputSeriesDto> GetThroughputAsync(string? range, string? service, CancellationToken ct = default)
    {
        var (windowSeconds, bucketSeconds) = ResolveRange(range);
        var fromUtc = DateTime.UtcNow.AddSeconds(-windowSeconds);
        var buckets = await _store.QueryThroughputAsync(fromUtc, bucketSeconds, service, ct);

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
