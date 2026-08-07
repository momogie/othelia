using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Observability;
using Othelia.Api.Services;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/queries")]
public sealed class QueriesController : ControllerBase
{
    private readonly ITracingService _tracing;

    public QueriesController(ITracingService tracing) => _tracing = tracing;

    [HttpGet("expensive")]
    public async Task<IActionResult> GetExpensiveQueries(
        [FromQuery] string? service,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] long? thresholdMs,
        [FromQuery] int? limit,
        [FromQuery] int offset,
        [FromQuery] string? sort,
        CancellationToken ct)
    {
        var query = new ExpensiveQueryQuery
        {
            Service = Normalize(service),
            FromUtc = from,
            ToUtc = to,
            MinDurationUs = thresholdMs is > 0 ? thresholdMs.Value * 1000L : 0,
            Limit = limit ?? 100,
            Offset = offset,
            Sort = Normalize(sort),
        };

        var result = await _tracing.GetExpensiveQueriesAsync(query, ct);
        Response.Headers["X-Total-Count"] = result.Total.ToString();
        return Ok(result.Items);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
