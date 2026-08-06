using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Observability;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly ITelemetryStore _store;

    public HealthController(ITelemetryStore store) => _store = store;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        var healthy = await _store.IsHealthyAsync(cts.Token);

        if (!healthy)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Status = "degraded",
                Service = "Othelia.Api",
                Database = "unavailable",
                Timestamp = DateTimeOffset.UtcNow,
            });

        return Ok(new
        {
            Status = "healthy",
            Service = "Othelia.Api",
            Database = "ok",
            Timestamp = DateTimeOffset.UtcNow,
        });
    }
}
