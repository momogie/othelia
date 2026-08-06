namespace Othelia.Api.Observability;

public sealed class RetentionService : BackgroundService
{
    private readonly ITelemetryStore _store;
    private readonly ITracingOptionsResolver _resolver;
    private readonly ILogger<RetentionService> _logger;

    public RetentionService(
        ITelemetryStore store,
        ITracingOptionsResolver resolver,
        ILogger<RetentionService> logger)
    {
        _store = store;
        _resolver = resolver;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retention cleanup failed.");
            }

            var options = await _resolver.ResolveAsync(stoppingToken);
            var interval = TimeSpan.FromHours(Math.Max(1, options.Retention.CleanupIntervalHours));
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var options = await _resolver.ResolveAsync(ct);
        if (!options.Retention.Enabled || options.Retention.RetentionDays <= 0)
            return;

        var cutoff = DateTime.UtcNow.AddDays(-options.Retention.RetentionDays);
        var deleted = await _store.DeleteSpansOlderThanAsync(cutoff, ct);
        if (deleted > 0)
            _logger.LogInformation("Retention cleanup deleted {Count} spans older than {Days} days.",
                deleted, options.Retention.RetentionDays);
    }
}
