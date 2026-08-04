using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Services;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/services")]
public sealed class ServicesController : ControllerBase
{
    private readonly ITracingService _tracing;

    public ServicesController(ITracingService tracing) => _tracing = tracing;

    [HttpGet]
    public async Task<IActionResult> GetServices(CancellationToken ct)
        => Ok(await _tracing.GetServicesAsync(ct));
}
