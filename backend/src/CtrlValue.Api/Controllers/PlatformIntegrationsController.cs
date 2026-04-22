using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.Interfaces;

namespace CtrlValue.Api.Controllers;

/// <summary>
/// Super-admin endpoint for managing platform-level (admin-fallback) API keys.
/// These keys are used as Tier 2 fallback when a user has no personal API key configured.
/// </summary>
[ApiController]
[Route("api/platform-integrations")]
[Authorize(Policy = "SuperAdmin")]
public class PlatformIntegrationsController : ControllerBase
{
    private readonly IEntityIntegrationService _integrationService;

    public PlatformIntegrationsController(IEntityIntegrationService integrationService)
    {
        _integrationService = integrationService;
    }

    /// <summary>Returns all platform integration configs. API keys are masked (HasApiKey flag only).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _integrationService.GetPlatformIntegrationsAsync();
        return Ok(result);
    }

    /// <summary>Creates or updates a platform integration key.</summary>
    [HttpPut("{integrationType}")]
    public async Task<IActionResult> Upsert(string integrationType, [FromBody] UpsertPlatformIntegrationRequest request)
    {
        var validTypes = new[] { "ALPHA_VANTAGE", "METALS_API", "COINGECKO", "OPENAI", "ANTHROPIC" };
        if (!validTypes.Contains(integrationType.ToUpper()))
            return BadRequest(new { error = $"Invalid integration type. Valid types: {string.Join(", ", validTypes)}" });

        var result = await _integrationService.UpsertPlatformIntegrationAsync(
            integrationType.ToUpper(), request.ApiKey, request.IsEnabled);

        return Ok(result);
    }

    /// <summary>Disables and removes the platform key for the given integration type.</summary>
    [HttpDelete("{integrationType}")]
    public async Task<IActionResult> Delete(string integrationType)
    {
        await _integrationService.DeletePlatformIntegrationAsync(integrationType.ToUpper());
        return NoContent();
    }
}

public class UpsertPlatformIntegrationRequest
{
    /// <summary>New API key. Omit or send null to keep the existing key unchanged.</summary>
    public string? ApiKey { get; set; }
    public bool IsEnabled { get; set; }
}
