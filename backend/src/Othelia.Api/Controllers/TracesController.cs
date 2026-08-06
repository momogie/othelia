using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Observability;
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
        [FromQuery] string? status,
        [FromQuery] string? name,
        [FromQuery] string? nameNot,
        [FromQuery] string? path,
        [FromQuery] string? pathNot,
        [FromQuery] long? minDurationMs,
        [FromQuery] long? maxDurationMs,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? limit,
        [FromQuery] int offset,
        [FromQuery] string? sort,
        CancellationToken ct)
    {
        var query = new TraceQuery
        {
            Service = Normalize(service),
            TraceId = Normalize(traceId),
            Status = Normalize(status),
            Name = Normalize(name),
            NameNot = Normalize(nameNot),
            Path = Normalize(path),
            PathNot = Normalize(pathNot),
            MinDurationMs = minDurationMs,
            MaxDurationMs = maxDurationMs,
            FromUtc = from,
            ToUtc = to,
            Limit = limit ?? 100,
            Offset = offset,
            Sort = Normalize(sort),
        };

        var result = await _tracing.GetTracesAsync(query, ct);
        Response.Headers["X-Total-Count"] = result.Total.ToString();
        return Ok(result.Items);
    }

    [HttpGet("{traceId}/spans")]
    public async Task<IActionResult> GetSpans([FromRoute] string traceId, CancellationToken ct)
        => Ok(await _tracing.GetSpansAsync(traceId, ct));

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
