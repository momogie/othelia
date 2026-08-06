using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Services;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/service-map")]
public sealed class ServiceMapController : ControllerBase
{
    private readonly IInsightsService _insights;

    public ServiceMapController(IInsightsService insights) => _insights = insights;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? service, CancellationToken ct)
        => Ok(await _insights.GetServiceMapAsync(service, ct));
}
