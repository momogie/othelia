using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using Othelia.Api.Observability;
using System.IO.Compression;
using System.Text;

namespace Othelia.Api;

public static class OtlpEndpoints
{
    public static void MapOtlpEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1").WithTags("OTLP");

        group.MapPost("/traces", HandleTracesAsync)
            .WithName("ExportTraces");

        group.MapPost("/metrics", HandleMetricsAsync)
            .WithName("ExportMetrics");

        group.MapPost("/logs", HandleLogsAsync)
            .WithName("ExportLogs");
    }

    private static async Task<IResult> HandleTracesAsync(HttpContext context, ITelemetryStore store, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Otlp.Traces");
        try
        {
            var request = await ReadRequestAsync<ExportTraceServiceRequest>(context.Request, context.RequestAborted);
            var spans = OtlpTraceConverter.ConvertRequest(request);
            await store.InsertSpansAsync(spans, context.RequestAborted);
            return Results.Bytes(Array.Empty<byte>(), "application/x-protobuf");
        }
        catch (UnsupportedContentTypeException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status415UnsupportedMediaType);
        }
        catch (Exception ex) when (ex is InvalidProtocolBufferException or InvalidDataException or FormatException)
        {
            return Results.Problem("Invalid OTLP trace payload.", statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest OTLP traces.");
            return Results.Problem("Failed to ingest traces.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> HandleMetricsAsync(HttpContext context, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Otlp.Metrics");
        try
        {
            var request = await ReadRequestAsync<ExportMetricsServiceRequest>(context.Request, context.RequestAborted);
            var count = request.ResourceMetrics.Sum(rm => rm.ScopeMetrics.Sum(sm => sm.Metrics.Count));
            logger.LogInformation("Received {Count} metrics (not persisted in Phase 1).", count);
            return Results.Bytes(Array.Empty<byte>(), "application/x-protobuf");
        }
        catch (UnsupportedContentTypeException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status415UnsupportedMediaType);
        }
        catch (Exception ex) when (ex is InvalidProtocolBufferException or InvalidDataException or FormatException)
        {
            return Results.Problem("Invalid OTLP metrics payload.", statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest OTLP metrics.");
            return Results.Problem("Failed to ingest metrics.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> HandleLogsAsync(HttpContext context, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Otlp.Logs");
        try
        {
            var request = await ReadRequestAsync<ExportLogsServiceRequest>(context.Request, context.RequestAborted);
            var count = request.ResourceLogs.Sum(rl => rl.ScopeLogs.Sum(sl => sl.LogRecords.Count));
            logger.LogInformation("Received {Count} log records (not persisted in Phase 1).", count);
            return Results.Bytes(Array.Empty<byte>(), "application/x-protobuf");
        }
        catch (UnsupportedContentTypeException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status415UnsupportedMediaType);
        }
        catch (Exception ex) when (ex is InvalidProtocolBufferException or InvalidDataException or FormatException)
        {
            return Results.Problem("Invalid OTLP logs payload.", statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest OTLP logs.");
            return Results.Problem("Failed to ingest logs.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<T> ReadRequestAsync<T>(HttpRequest request, CancellationToken ct)
        where T : IMessage<T>, new()
    {
        var contentType = request.ContentType ?? string.Empty;
        var isGzip = contentType.Contains("gzip", StringComparison.OrdinalIgnoreCase);
        var isProtobuf = contentType.Contains("protobuf", StringComparison.OrdinalIgnoreCase)
                        || string.IsNullOrWhiteSpace(contentType);
        var isJson = contentType.Contains("json", StringComparison.OrdinalIgnoreCase);

        var parser = new MessageParser<T>(() => new T());
        Stream body = request.Body;

        if (isGzip)
        {
            try
            {
                body = new GZipStream(request.Body, CompressionMode.Decompress);
            }
            catch (InvalidDataException ex)
            {
                throw new UnsupportedContentTypeException("Invalid gzip payload.", ex);
            }
        }

        if (isProtobuf)
        {
            using var buffer = new MemoryStream();
            await body.CopyToAsync(buffer, ct);
            return parser.ParseFrom(buffer.ToArray());
        }

        if (isJson)
        {
            using var buffer = new MemoryStream();
            await body.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                bytes = bytes[3..];
            return parser.ParseJson(Encoding.UTF8.GetString(bytes));
        }

        throw new UnsupportedContentTypeException($"Unsupported OTLP content type '{contentType}'.");
    }

    private sealed class UnsupportedContentTypeException : Exception
    {
        public UnsupportedContentTypeException(string message, Exception? inner = null) : base(message, inner) { }
    }
}
