using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Services;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public AlertsController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? service, CancellationToken ct)
        => Ok(await _dashboard.GetAlertsAsync(service, ct));

    [HttpPatch("{id}/ack")]
    public async Task<IActionResult> Acknowledge([FromRoute] string id, CancellationToken ct)
    {
        await _dashboard.AcknowledgeAlertAsync(id, ct);
        return NoContent();
    }
}
