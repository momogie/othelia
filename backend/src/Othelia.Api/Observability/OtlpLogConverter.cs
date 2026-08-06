using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using System.Text.Json;

namespace Othelia.Api.Observability;

/// <summary>
/// Mengubah ExportLogsServiceRequest (OTLP) menjadi LogRecord.
/// Hanya severity WARN+ (SeverityNumber >= 13) yang dipertahankan.
/// </summary>
public static class OtlpLogConverter
{
    private const int MinSeverityNumber = 13; // WARN
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<LogRecord> ConvertRequest(ExportLogsServiceRequest request)
    {
        var logs = new List<LogRecord>();
        foreach (var resourceLog in request.ResourceLogs)
        {
            var resourceAttributes = CollectAttributes(resourceLog.Resource?.Attributes);
            var serviceName = GetString(resourceAttributes, "service.name") ?? "unknown";
            var serviceVersion = GetString(resourceAttributes, "service.version");
            var serviceEnvironment = GetString(resourceAttributes, "deployment.environment.name")
                                    ?? GetString(resourceAttributes, "deployment.environment");
            var resourceJson = resourceAttributes.Count == 0
                ? null
                : JsonSerializer.Serialize(resourceAttributes, JsonOptions);

            foreach (var scopeLog in resourceLog.ScopeLogs)
            {
                foreach (var record in scopeLog.LogRecords)
                {
                    if ((int)record.SeverityNumber < MinSeverityNumber)
                        continue;

                    var attributes = CollectAttributes(record.Attributes);
                    logs.Add(new LogRecord
                    {
                        TimestampUtc = NsToUtc(record.TimeUnixNano),
                        ServiceName = serviceName,
                        ServiceVersion = serviceVersion,
                        ServiceEnvironment = serviceEnvironment,
                        SeverityText = string.IsNullOrEmpty(record.SeverityText)
                            ? record.SeverityNumber.ToString()
                            : record.SeverityText,
                        SeverityNumber = (int)record.SeverityNumber,
                        Body = BodyToString(record.Body),
                        TraceId = record.TraceId.IsEmpty ? null : record.TraceId.ToByteArray().ToHex(),
                        SpanId = record.SpanId.IsEmpty ? null : record.SpanId.ToByteArray().ToHex(),
                        AttributesJson = attributes.Count == 0 ? null : JsonSerializer.Serialize(attributes, JsonOptions),
                        ResourceJson = resourceJson,
                    });
                }
            }
        }
        return logs;
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
            }
        }
        return dict;
    }

    private static string? BodyToString(AnyValue? body)
    {
        if (body is null)
            return null;

        return body.ValueCase switch
        {
            AnyValue.ValueOneofCase.StringValue => body.StringValue,
            AnyValue.ValueOneofCase.BoolValue => body.BoolValue ? "true" : "false",
            AnyValue.ValueOneofCase.IntValue => body.IntValue.ToString(),
            AnyValue.ValueOneofCase.DoubleValue => body.DoubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.BytesValue => Convert.ToBase64String(body.BytesValue.ToByteArray()),
            _ => body.ToString(),
        };
    }

    private static DateTime NsToUtc(ulong unixNano) =>
        DateTime.UnixEpoch.AddTicks((long)(unixNano / 100));

    private static string ToHex(this byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    private static string? GetString(IReadOnlyDictionary<string, object?> attributes, string key) =>
        attributes.TryGetValue(key, out var value) ? value as string : null;
}
