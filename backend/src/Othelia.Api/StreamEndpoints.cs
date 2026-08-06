using Othelia.Api.Observability;
using Othelia.Api.Services;
using System.Text.Json;

namespace Othelia.Api;

public static class StreamEndpoints
{
    public static void MapStreamEndpoints(this WebApplication app)
    {
        app.MapGet("/api/stream", HandleStreamAsync)
            .WithName("StreamDashboard")
            .RequireAuthorization();
    }

    private static async Task HandleStreamAsync(
        HttpContext context,
        IDashboardService dashboard,
        ITracingService tracing,
        ITracingOptionsResolver resolver,
        ILoggerFactory loggerFactory,
        string? service,
        string? range,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Sse.Dashboard");
        var options = await resolver.ResolveAsync(ct);
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Live.StreamIntervalSeconds));
        var windowSeconds = Math.Max(60, options.Query.DashboardWindowSeconds);

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var fromUtc = DateTime.UtcNow.AddSeconds(-windowSeconds);
                var d = await dashboard.GetAsync(service, ct);
                var traces = await tracing.GetTracesAsync(new TraceQuery
                {
                    FromUtc = fromUtc,
                    Service = service,
                    Limit = 5,
                    Sort = "start_desc",
                }, ct);
                var rangeSeries = await dashboard.GetThroughputAsync(string.IsNullOrWhiteSpace(range) ? "1m" : range.Trim(), service, ct);

                var payload = JsonSerializer.Serialize(new
                {
                    dashboard = d,
                    traces = traces.Items,
                    throughput = rangeSeries,
                });
                await context.Response.WriteAsync($"data: {payload}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SSE dashboard tick failed.");
            }

            try
            {
                await Task.Delay(interval, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
