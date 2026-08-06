using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Services;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? service, CancellationToken ct)
        => Ok(await _dashboard.GetAsync(service, ct));

    [HttpGet("throughput")]
    public async Task<IActionResult> GetThroughput(
        [FromQuery] string? range,
        [FromQuery] string? service,
        CancellationToken ct)
        => Ok(await _dashboard.GetThroughputAsync(range, service, ct));
}
