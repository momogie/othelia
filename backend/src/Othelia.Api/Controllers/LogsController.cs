using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Observability;
using Othelia.Api.Services;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/logs")]
public sealed class LogsController : ControllerBase
{
    private readonly ITracingService _tracing;

    public LogsController(ITracingService tracing) => _tracing = tracing;

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? service,
        [FromQuery] string? severity,
        [FromQuery] string? search,
        [FromQuery] string? traceId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? limit,
        [FromQuery] int offset,
        [FromQuery] string? sort,
        CancellationToken ct)
    {
        var query = new LogQuery
        {
            Service = Normalize(service),
            Severity = Normalize(severity),
            Search = Normalize(search),
            TraceId = Normalize(traceId),
            FromUtc = from,
            ToUtc = to,
            Limit = limit ?? 100,
            Offset = offset,
            Sort = Normalize(sort),
        };

        var result = await _tracing.GetLogsAsync(query, ct);
        Response.Headers["X-Total-Count"] = result.Total.ToString();
        return Ok(result.Items);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
