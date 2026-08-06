using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Observability;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly ITracingOptionsResolver _resolver;
    private readonly ISettingsStore _settings;

    public SettingsController(ITracingOptionsResolver resolver, ISettingsStore settings)
    {
        _resolver = resolver;
        _settings = settings;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => Ok(await _resolver.ResolveAsync(ct));

    [HttpPut]
    public async Task<IActionResult> PutSettings([FromBody] TracingSettingsPatch patch, CancellationToken ct)
    {
        if (!await _settings.IsAvailableAsync())
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Settings persistence is not configured (no SQL storage)." });

        if (patch is null)
            return BadRequest(new { error = "Request body is required." });

        var validationError = Validate(patch);
        if (validationError is not null)
            return BadRequest(new { error = validationError });

        await _resolver.SaveAsync(patch, ct);
        return Ok(await _resolver.ResolveAsync(ct));
    }

    private static string? Validate(TracingSettingsPatch patch)
    {
        if (patch.QueryMaxTracesPerRequest is < 1)
            return "QueryMaxTracesPerRequest must be at least 1.";
        if (patch.QueryMaxSpansPerTrace is < 1)
            return "QueryMaxSpansPerTrace must be at least 1.";
        if (patch.QueryDefaultLookbackSeconds is < 0)
            return "QueryDefaultLookbackSeconds must not be negative.";
        if (patch.QuerySlowThresholdMs is < 1)
            return "QuerySlowThresholdMs must be at least 1.";
        if (patch.AlertsMaxAlerts is < 1)
            return "AlertsMaxAlerts must be at least 1.";
        if (patch.AlertsErrorRateDownThreshold is < 0)
            return "AlertsErrorRateDownThreshold must not be negative.";
        if (patch.MetricsDefaultBucketSeconds is < 1)
            return "MetricsDefaultBucketSeconds must be at least 1.";
        if (patch.MetricsMaxSeriesPoints is < 1)
            return "MetricsMaxSeriesPoints must be at least 1.";
        if (patch.ServiceMapWindowSeconds is < 1)
            return "ServiceMapWindowSeconds must be at least 1.";
        if (patch.CollectorsWindowSeconds is < 1)
            return "CollectorsWindowSeconds must be at least 1.";
        if (patch.CollectorsStaleMinutes is < 1)
            return "CollectorsStaleMinutes must be at least 1.";
        if (patch.LiveStreamIntervalSeconds is < 1)
            return "LiveStreamIntervalSeconds must be at least 1.";
        if (patch.RetentionDays is < 0)
            return "RetentionDays must not be negative.";
        if (patch.RetentionCleanupIntervalHours is < 1)
            return "RetentionCleanupIntervalHours must be at least 1.";
        return null;
    }

    [HttpDelete]
    public async Task<IActionResult> ResetSettings(CancellationToken ct)
    {
        if (!await _settings.IsAvailableAsync())
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Settings persistence is not configured (no SQL storage)." });

        await _resolver.ResetAsync(ct);
        return Ok(await _resolver.ResolveAsync(ct));
    }
}
