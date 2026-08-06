using Microsoft.AspNetCore.Mvc;
using Othelia.Api.Observability;

namespace Othelia.Api.Controllers;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly ITracingOptionsResolver _resolver;
    private readonly ISettingsStore _settings;

    public SettingsController(ITracingOptionsResolver resolver, ISettingsStore settings)
    {
        _resolver = resolver;
        _settings = settings;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => Ok(await _resolver.ResolveAsync(ct));

    [HttpPut]
    public async Task<IActionResult> PutSettings([FromBody] TracingSettingsPatch patch, CancellationToken ct)
    {
        if (!await _settings.IsAvailableAsync())
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Settings persistence is not configured (no SQL storage)." });

        if (patch is null)
            return BadRequest(new { error = "Request body is required." });

        await _resolver.SaveAsync(patch, ct);
        return Ok(await _resolver.ResolveAsync(ct));
    }

    [HttpDelete]
    public async Task<IActionResult> ResetSettings(CancellationToken ct)
    {
        if (!await _settings.IsAvailableAsync())
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Settings persistence is not configured (no SQL storage)." });

        await _resolver.ResetAsync(ct);
        return Ok(await _resolver.ResolveAsync(ct));
    }
}
