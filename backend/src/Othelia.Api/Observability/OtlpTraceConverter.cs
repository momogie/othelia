using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Trace.V1;
using System.Text.Json;

namespace Othelia.Api.Observability;

public static class OtlpTraceConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<SpanRecord> ConvertRequest(ExportTraceServiceRequest request)
    {
        var spans = new List<SpanRecord>();
        foreach (var resourceSpan in request.ResourceSpans)
        {
            var resourceAttributes = CollectAttributes(resourceSpan.Resource?.Attributes);
            var serviceName = GetString(resourceAttributes, "service.name") ?? "unknown";
            var serviceVersion = GetString(resourceAttributes, "service.version");
            var serviceEnvironment = GetString(resourceAttributes, "deployment.environment.name")
                                    ?? GetString(resourceAttributes, "deployment.environment");

            foreach (var scopeSpan in resourceSpan.ScopeSpans)
            {
                foreach (var span in scopeSpan.Spans)
                {
                    var attributes = CollectAttributes(span.Attributes);
                    var traceId = span.TraceId.ToByteArray().ToHex();
                    var spanId = span.SpanId.ToByteArray().ToHex();
                    var parentSpanId = span.ParentSpanId is null || span.ParentSpanId.IsEmpty
                        ? null
                        : span.ParentSpanId.ToByteArray().ToHex();

                    var startTimeUtc = NsToUtc(span.StartTimeUnixNano);
                    var endTimeUtc = NsToUtc(span.EndTimeUnixNano);
                    var durationUs = span.EndTimeUnixNano > span.StartTimeUnixNano
                        ? (long)((span.EndTimeUnixNano - span.StartTimeUnixNano) / 1000)
                        : 0L;

                    var statusCode = span.Status?.Code ?? Status.Types.StatusCode.Unset;
                    var statusMessage = string.IsNullOrEmpty(span.Status?.Message) ? null : span.Status!.Message;

                    spans.Add(new SpanRecord
                    {
                        TraceId = traceId,
                        SpanId = spanId,
                        ParentSpanId = parentSpanId,
                        Name = span.Name,
                        ServiceName = serviceName,
                        Kind = span.Kind.ToString(),
                        StartTimeUtc = startTimeUtc,
                        EndTimeUtc = endTimeUtc,
                        DurationUs = durationUs,
                        StatusCode = statusCode.ToString(),
                        StatusMessage = statusMessage,
                        HttpMethod = GetString(attributes, "http.request.method") ?? GetString(attributes, "http.method"),
                        HttpPath = GetString(attributes, "url.path") ?? GetString(attributes, "http.route"),
                        HttpStatusCode = GetInt(attributes, "http.response.status_code") ?? GetInt(attributes, "http.status_code"),
                        ServiceVersion = serviceVersion,
                        ServiceEnvironment = serviceEnvironment,
                        ResourceJson = resourceAttributes.Count == 0 ? null : JsonSerializer.Serialize(resourceAttributes, JsonOptions),
                        AttributesJson = attributes.Count == 0 ? null : JsonSerializer.Serialize(attributes, JsonOptions),
                        EventsJson = SerializeEvents(span),
                        LinksJson = SerializeLinks(span),
                    });
                }
            }
        }
        return spans;
    }

    private static Dictionary<string, object?> CollectAttributes(IEnumerable<KeyValue>? attributes)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (attributes is null)
            return dict;

        foreach (var kv in attributes)
        {
            var value = kv.Value;
            switch (value?.ValueCase)
            {
                case AnyValue.ValueOneofCase.StringValue:
                    dict[kv.Key] = value.StringValue;
                    break;
                case AnyValue.ValueOneofCase.BoolValue:
                    dict[kv.Key] = value.BoolValue;
                    break;
                case AnyValue.ValueOneofCase.IntValue:
                    dict[kv.Key] = value.IntValue;
                    break;
                case AnyValue.ValueOneofCase.DoubleValue:
                    dict[kv.Key] = value.DoubleValue;
                    break;
                default:
                    break;
            }
        }
        return dict;
    }

    private static string? SerializeEvents(Span span)
    {
        if (span.Events.Count == 0)
            return null;

        var events = span.Events.Select(e => new
        {
            name = e.Name,
            timeUnixNano = e.TimeUnixNano,
            timeUtc = NsToUtc(e.TimeUnixNano),
            attributes = CollectAttributes(e.Attributes),
        });
        return JsonSerializer.Serialize(events, JsonOptions);
    }

    private static string? SerializeLinks(Span span)
    {
        if (span.Links.Count == 0)
            return null;

        var links = span.Links.Select(l => new
        {
            traceId = l.TraceId.ToByteArray().ToHex(),
            spanId = l.SpanId.ToByteArray().ToHex(),
            attributes = CollectAttributes(l.Attributes),
        });
        return JsonSerializer.Serialize(links, JsonOptions);
    }

    private static DateTime NsToUtc(ulong unixNano) =>
        DateTime.UnixEpoch.AddTicks((long)(unixNano / 100));

    private static string ToHex(this byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    private static string? GetString(IReadOnlyDictionary<string, object?> attributes, string key) =>
        attributes.TryGetValue(key, out var value) ? value as string : null;

    private static int? GetInt(IReadOnlyDictionary<string, object?> attributes, string key)
    {
        if (!attributes.TryGetValue(key, out var value))
            return null;

        return value switch
        {
            long l => (int)l,
            int i => i,
            double d => (int)d,
            _ => null,
        };
    }
}
