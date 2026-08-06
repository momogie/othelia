using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using System.Text.Json;

namespace Othelia.Api.Observability;

public static class OtlpMetricConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<MetricRecord> ConvertRequest(ExportMetricsServiceRequest request)
    {
        var metrics = new List<MetricRecord>();
        foreach (var resourceMetric in request.ResourceMetrics)
        {
            var resourceAttributes = CollectAttributes(resourceMetric.Resource?.Attributes);
            var serviceName = GetString(resourceAttributes, "service.name") ?? "unknown";
            var serviceVersion = GetString(resourceAttributes, "service.version");
            var serviceEnvironment = GetString(resourceAttributes, "deployment.environment.name")
                                    ?? GetString(resourceAttributes, "deployment.environment");
            var resourceJson = resourceAttributes.Count == 0 ? null : JsonSerializer.Serialize(resourceAttributes, JsonOptions);

            foreach (var scopeMetric in resourceMetric.ScopeMetrics)
            {
                foreach (var metric in scopeMetric.Metrics)
                {
                    AddPoints(metrics, metric, metric.Gauge?.DataPoints, "gauge", serviceName, serviceVersion, serviceEnvironment, resourceJson);
                    AddPoints(metrics, metric, metric.Sum?.DataPoints, "sum", serviceName, serviceVersion, serviceEnvironment, resourceJson);
                    AddPoints(metrics, metric, metric.Histogram?.DataPoints, "histogram", serviceName, serviceVersion, serviceEnvironment, resourceJson);
                    AddPoints(metrics, metric, metric.Summary?.DataPoints, "summary", serviceName, serviceVersion, serviceEnvironment, resourceJson);
                }
            }
        }
        return metrics;
    }

    private static void AddPoints(
        List<MetricRecord> metrics,
        Metric metric,
        IEnumerable<NumberDataPoint>? points,
        string type,
        string serviceName, string? serviceVersion, string? serviceEnvironment, string? resourceJson)
    {
        if (points is null)
            return;

        foreach (var point in points)
        {
            var value = point.ValueCase switch
            {
                NumberDataPoint.ValueOneofCase.AsDouble => point.AsDouble,
                NumberDataPoint.ValueOneofCase.AsInt => point.AsInt,
                _ => 0.0,
            };

            var attributes = CollectAttributes(point.Attributes);
            metrics.Add(new MetricRecord
            {
                TimestampUtc = NsToUtc(point.TimeUnixNano),
                ServiceName = serviceName,
                ServiceVersion = serviceVersion,
                ServiceEnvironment = serviceEnvironment,
                Name = metric.Name,
                Type = type,
                Unit = string.IsNullOrEmpty(metric.Unit) ? null : metric.Unit,
                Value = value,
                AttributesJson = attributes.Count == 0 ? null : JsonSerializer.Serialize(attributes, JsonOptions),
                ResourceJson = resourceJson,
            });
        }
    }

    private static void AddPoints(
        List<MetricRecord> metrics,
        Metric metric,
        IEnumerable<HistogramDataPoint>? points,
        string type,
        string serviceName, string? serviceVersion, string? serviceEnvironment, string? resourceJson)
    {
        if (points is null)
            return;

        foreach (var point in points)
        {
            var attributes = CollectAttributes(point.Attributes);
            metrics.Add(new MetricRecord
            {
                TimestampUtc = NsToUtc(point.TimeUnixNano),
                ServiceName = serviceName,
                ServiceVersion = serviceVersion,
                ServiceEnvironment = serviceEnvironment,
                Name = metric.Name,
                Type = type,
                Unit = string.IsNullOrEmpty(metric.Unit) ? null : metric.Unit,
                Value = point.Sum,
                Count = (long)point.Count,
                AttributesJson = attributes.Count == 0 ? null : JsonSerializer.Serialize(attributes, JsonOptions),
                ResourceJson = resourceJson,
            });
        }
    }

    private static void AddPoints(
        List<MetricRecord> metrics,
        Metric metric,
        IEnumerable<SummaryDataPoint>? points,
        string type,
        string serviceName, string? serviceVersion, string? serviceEnvironment, string? resourceJson)
    {
        if (points is null)
            return;

        foreach (var point in points)
        {
            var attributes = CollectAttributes(point.Attributes);
            metrics.Add(new MetricRecord
            {
                TimestampUtc = NsToUtc(point.TimeUnixNano),
                ServiceName = serviceName,
                ServiceVersion = serviceVersion,
                ServiceEnvironment = serviceEnvironment,
                Name = metric.Name,
                Type = type,
                Unit = string.IsNullOrEmpty(metric.Unit) ? null : metric.Unit,
                Value = point.Sum,
                Count = (long)point.Count,
                AttributesJson = attributes.Count == 0 ? null : JsonSerializer.Serialize(attributes, JsonOptions),
                ResourceJson = resourceJson,
            });
        }
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

    private static DateTime NsToUtc(ulong unixNano) =>
        DateTime.UnixEpoch.AddTicks((long)(unixNano / 100));

    private static string? GetString(IReadOnlyDictionary<string, object?> attributes, string key) =>
        attributes.TryGetValue(key, out var value) ? value as string : null;
}
