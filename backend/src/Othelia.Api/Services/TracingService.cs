using Othelia.Api.Models;
using Othelia.Api.Observability;
using System.Text.Json;

namespace Othelia.Api.Services;

public sealed class TracingService : ITracingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITelemetryStore _store;
    private readonly ITracingOptionsResolver _resolver;

    public TracingService(ITelemetryStore store, ITracingOptionsResolver resolver)
    {
        _store = store;
        _resolver = resolver;
    }

    public async Task<IReadOnlyList<ServiceDto>> GetServicesAsync(CancellationToken ct = default)
    {
        var opts = await _resolver.ResolveAsync(ct);
        var from = DateTime.UtcNow.AddSeconds(-Math.Max(0, opts.Query.DefaultLookbackSeconds));
        var rows = await _store.QueryServicesAsync(from, ct);

        return rows.Select(r => new ServiceDto
        {
            Name = r.ServiceName,
            Version = r.ServiceVersion,
            Environment = r.ServiceEnvironment,
            LastSeen = new DateTimeOffset(r.LastSeenUtc, TimeSpan.Zero),
            TotalTraces = r.TotalTraces,
            ErrorCount = r.ErrorCount,
        }).ToList();
    }

    public async Task<TraceQueryResult> GetTracesAsync(
        TraceQuery query,
        CancellationToken ct = default)
    {
        var opts = await _resolver.ResolveAsync(ct);
        var maxLimit = Math.Max(1, opts.Query.MaxTracesPerRequest);
        var effectiveQuery = query with
        {
            FromUtc = query.FromUtc ?? DateTime.UtcNow.AddSeconds(-Math.Max(0, opts.Query.DefaultLookbackSeconds)),
            SlowThresholdMs = query.SlowThresholdMs ?? Math.Max(1, opts.Query.SlowThresholdMs),
            Limit = Math.Clamp(query.Limit > 0 ? query.Limit : maxLimit, 1, maxLimit),
        };

        var rows = await _store.QueryRecentTracesAsync(effectiveQuery, ct);

        var items = rows.Select(r => new TraceSummaryDto
        {
            TraceId = r.TraceId,
            Name = string.IsNullOrEmpty(r.RootName) ? "(untitled)" : r.RootName,
            RootServiceName = string.IsNullOrEmpty(r.RootService) ? "unknown" : r.RootService,
            StartTime = new DateTimeOffset(r.StartTimeUtc, TimeSpan.Zero),
            Duration = TimeSpan.FromMilliseconds(r.DurationUs / 1000.0),
            SpanCount = r.SpanCount,
            Status = r.HasError
                ? "error"
                : r.DurationUs >= effectiveQuery.SlowThresholdMs * 1000L
                    ? "slow"
                    : "ok",
            Tags = r.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries),
        }).ToList();

        return new TraceQueryResult { Items = items, Total = rows.FirstOrDefault()?.Total ?? 0 };
    }

    public async Task<IReadOnlyList<SpanDto>> GetSpansAsync(string traceId, CancellationToken ct = default)
    {
        var opts = await _resolver.ResolveAsync(ct);
        var rows = await _store.QuerySpansAsync(traceId, Math.Max(1, opts.Query.MaxSpansPerTrace), ct);

        return rows.Select(s => new SpanDto
        {
            SpanId = s.SpanId,
            ParentSpanId = s.ParentSpanId,
            TraceId = s.TraceId,
            Name = s.Name,
            ServiceName = s.ServiceName,
            Kind = s.Kind,
            StartTime = new DateTimeOffset(s.StartTimeUtc, TimeSpan.Zero),
            Duration = TimeSpan.FromMilliseconds(s.DurationUs / 1000.0),
            Status = s.StatusCode,
            Attributes = DeserializeAttributes(s.AttributesJson),
            ResourceAttributes = DeserializeAttributes(s.ResourceJson),
            Events = DeserializeEvents(s.EventsJson),
            Links = DeserializeLinks(s.LinksJson),
        }).ToList();
    }

    public async Task<LogQueryResult> GetLogsAsync(LogQuery query, CancellationToken ct = default)
    {
        var opts = await _resolver.ResolveAsync(ct);
        var maxLimit = Math.Max(1, opts.Query.MaxTracesPerRequest);
        var effectiveQuery = query with
        {
            FromUtc = query.FromUtc ?? DateTime.UtcNow.AddSeconds(-Math.Max(0, opts.Query.DefaultLookbackSeconds)),
            Limit = Math.Clamp(query.Limit > 0 ? query.Limit : maxLimit, 1, maxLimit),
        };

        var rows = await _store.QueryLogsAsync(effectiveQuery, ct);

        var items = rows.Select(r => new LogDto
        {
            Timestamp = new DateTimeOffset(r.TimestampUtc, TimeSpan.Zero),
            ServiceName = r.ServiceName,
            ServiceVersion = r.ServiceVersion,
            ServiceEnvironment = r.ServiceEnvironment,
            Severity = r.SeverityText,
            Body = r.Body,
            TraceId = r.TraceId,
            SpanId = r.SpanId,
            Attributes = DeserializeAttributes(r.AttributesJson),
        }).ToList();

        return new LogQueryResult { Items = items, Total = rows.FirstOrDefault()?.Total ?? 0 };
    }

    public async Task<ExpensiveQueryResult> GetExpensiveQueriesAsync(
        ExpensiveQueryQuery query,
        CancellationToken ct = default)
    {
        var opts = await _resolver.ResolveAsync(ct);
        var slowMs = Math.Max(1, opts.Query.SlowThresholdMs);
        var effectiveQuery = query with
        {
            FromUtc = query.FromUtc ?? DateTime.UtcNow.AddSeconds(-Math.Max(0, opts.Query.DefaultLookbackSeconds)),
            MinDurationUs = query.MinDurationUs > 0 ? query.MinDurationUs : slowMs * 1000L,
            Limit = Math.Clamp(query.Limit > 0 ? query.Limit : 100, 1, Math.Max(1, opts.Query.MaxTracesPerRequest)),
        };

        var rows = await _store.QueryExpensiveQueriesAsync(effectiveQuery, ct);

        var items = rows.Select(r => new ExpensiveQueryDto
        {
            Statement = r.Statement,
            Summary = r.Summary,
            ServiceName = r.ServiceName,
            Executions = r.Executions,
            AvgMs = Math.Round(r.AvgMs, 1),
            MaxMs = Math.Round(r.MaxMs, 1),
            TotalMs = Math.Round(r.TotalMs, 1),
            LastSeen = new DateTimeOffset(r.LastSeenUtc, TimeSpan.Zero),
            SampleTraceId = r.SampleTraceId,
            Status = r.MaxMs >= slowMs ? "slow" : "ok",
        }).ToList();

        return new ExpensiveQueryResult { Items = items, Total = rows.FirstOrDefault()?.Total ?? 0 };
    }

    private static IReadOnlyDictionary<string, string>? DeserializeAttributes(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions);
        return parsed?.ToDictionary(kv => kv.Key, kv => ValueToString(kv.Value));
    }

    private static IReadOnlyList<SpanEventDto>? DeserializeEvents(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var parsed = JsonSerializer.Deserialize<List<EventJson>>(json, JsonOptions);
        return parsed?.Select(e => new SpanEventDto
        {
            Name = e.Name ?? "(event)",
            Time = new DateTimeOffset(e.TimeUtc, TimeSpan.Zero),
            Attributes = e.Attributes?.ToDictionary(kv => kv.Key, kv => ValueToString(kv.Value)),
        }).ToList();
    }

    private static IReadOnlyList<SpanLinkDto>? DeserializeLinks(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var parsed = JsonSerializer.Deserialize<List<LinkJson>>(json, JsonOptions);
        return parsed?.Select(l => new SpanLinkDto
        {
            TraceId = l.TraceId ?? string.Empty,
            SpanId = l.SpanId ?? string.Empty,
            Attributes = l.Attributes?.ToDictionary(kv => kv.Key, kv => ValueToString(kv.Value)),
        }).ToList();
    }

    private static string ValueToString(object? value) => value switch
    {
        JsonElement element => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => element.GetRawText(),
        },
        string s => s,
        bool b => b.ToString(),
        null => string.Empty,
        _ => value.ToString() ?? string.Empty,
    };

    private sealed class EventJson
    {
        public string? Name { get; set; }
        public DateTime TimeUtc { get; set; }
        public Dictionary<string, object?>? Attributes { get; set; }
    }

    private sealed class LinkJson
    {
        public string? TraceId { get; set; }
        public string? SpanId { get; set; }
        public Dictionary<string, object?>? Attributes { get; set; }
    }
}
