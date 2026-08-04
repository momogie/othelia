using Microsoft.AspNetCore.Mvc;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        Status = "healthy",
        Service = "Othelia.Api",
        Timestamp = DateTimeOffset.UtcNow,
    });
}
