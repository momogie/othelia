using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Services;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/collectors")]
public sealed class CollectorsController : ControllerBase
{
    private readonly IInsightsService _insights;

    public CollectorsController(IInsightsService insights) => _insights = insights;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await _insights.GetCollectorsAsync(ct));
}
