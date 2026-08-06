using Microsoft.Extensions.Options;
using Othelia.Api.Models;
using Othelia.Api.Observability;
using System.Text.Json;

namespace Othelia.Api.Services;

public sealed class TracingService : ITracingService
{
    private readonly ITelemetryStore _store;
    private readonly TracingOptions _options;

    public TracingService(ITelemetryStore store, IOptions<TracingOptions> options)
    {
        _store = store;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<ServiceDto>> GetServicesAsync(CancellationToken ct = default)
    {
        var from = DateTime.UtcNow.AddSeconds(-_options.Query.DefaultLookbackSeconds);
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

    public async Task<IReadOnlyList<TraceSummaryDto>> GetTracesAsync(
        string? service = null,
        string? traceId = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        var from = DateTime.UtcNow.AddSeconds(-_options.Query.DefaultLookbackSeconds);
        var max = Math.Min(limit ?? _options.Query.MaxTracesPerRequest, _options.Query.MaxTracesPerRequest);
        var effectiveTraceId = string.IsNullOrWhiteSpace(traceId) ? null : traceId;

        var rows = await _store.QueryRecentTracesAsync(service, effectiveTraceId, from, max, ct);

        return rows.Select(r => new TraceSummaryDto
        {
            TraceId = r.TraceId,
            Name = string.IsNullOrEmpty(r.RootName) ? "(untitled)" : r.RootName,
            RootServiceName = string.IsNullOrEmpty(r.RootService) ? "unknown" : r.RootService,
            StartTime = new DateTimeOffset(r.StartTimeUtc, TimeSpan.Zero),
            Duration = TimeSpan.FromMilliseconds(r.DurationUs / 1000.0),
            SpanCount = r.SpanCount,
            Status = r.HasError ? "error" : "ok",
            Tags = r.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries),
        }).ToList();
    }

    public async Task<IReadOnlyList<SpanDto>> GetSpansAsync(string traceId, CancellationToken ct = default)
    {
        var rows = await _store.QuerySpansAsync(traceId, ct);

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
            Events = DeserializeEvents(s.EventsJson),
        }).ToList();
    }

    private static IReadOnlyDictionary<string, string>? DeserializeAttributes(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        return parsed?.ToDictionary(kv => kv.Key, kv => ValueToString(kv.Value));
    }

    private static IReadOnlyList<SpanEventDto>? DeserializeEvents(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var parsed = JsonSerializer.Deserialize<List<EventJson>>(json);
        return parsed?.Select(e => new SpanEventDto
        {
            Name = e.Name ?? "(event)",
            Time = new DateTimeOffset(e.TimeUtc, TimeSpan.Zero),
            Attributes = e.Attributes?.ToDictionary(kv => kv.Key, kv => ValueToString(kv.Value)),
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
}
