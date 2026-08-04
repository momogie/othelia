using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Services;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/traces")]
public sealed class TracesController : ControllerBase
{
    private readonly ITracingService _tracing;

    public TracesController(ITracingService tracing) => _tracing = tracing;

    [HttpGet]
    public async Task<IActionResult> GetTraces(
        [FromQuery] string? service,
        [FromQuery] string? traceId,
        [FromQuery] int? limit,
        CancellationToken ct)
        => Ok(await _tracing.GetTracesAsync(service, traceId, limit, ct));

    [HttpGet("{traceId}/spans")]
    public async Task<IActionResult> GetSpans([FromRoute] string traceId, CancellationToken ct)
        => Ok(await _tracing.GetSpansAsync(traceId, ct));
}
