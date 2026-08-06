using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Services;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/metrics")]
public sealed class MetricsController : ControllerBase
{
    private readonly IInsightsService _insights;

    public MetricsController(IInsightsService insights) => _insights = insights;

    [HttpGet]
    public async Task<IActionResult> GetNames([FromQuery] string? service, CancellationToken ct)
        => Ok(await _insights.GetMetricNamesAsync(service, ct));

    [HttpGet("query")]
    public async Task<IActionResult> Query(
        [FromQuery] string? metric,
        [FromQuery] string? service,
        [FromQuery] string? aggregation,
        [FromQuery] int? bucketSeconds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(metric))
            return BadRequest(new { error = "metric is required." });

        return Ok(await _insights.GetMetricSeriesAsync(metric, service, aggregation, bucketSeconds, from, to, ct));
    }
}
