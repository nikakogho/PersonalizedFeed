using Microsoft.AspNetCore.Mvc;
using PersonalizedFeed.Domain.Repositories;

namespace PersonalizedFeed.Api.Controllers;

[ApiController]
[Route("v1/debug")]
public class DebugController : ControllerBase
{
    private readonly IUserSignalsRepository _userSignalsRepository;
    private readonly ITenantConfigRepository _tenantConfigRepository;

    public DebugController(IUserSignalsRepository userSignalsRepository)
    {
        _userSignalsRepository = userSignalsRepository;
    }

    // GET /v1/debug/user-signals?userHash=userhash_123
    [HttpGet("user-signals")]
    public async Task<IActionResult> GetUserSignals(
        [FromHeader(Name = "X-Tenant-Id")] string? tenantId,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromQuery] string? userHash,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(userHash))
        {
            return BadRequest(new
            {
                error = "Missing X-Tenant-Id, X-Api-Key header or userHash query parameter."
            });
        }

        var signals = await _userSignalsRepository.GetByTenantAndUserHashAsync(tenantId, userHash, ct);

        if (signals is null)
        {
            return NotFound(new
            {
                tenantId,
                userHash,
                message = "UserSignals not found."
            });
        }

        // For debug purposes we can just return the domain object as-is
        return Ok(signals);
    }

    [HttpPost("set-personalization")]
    public async Task<IActionResult> SetPersonalization(
        [FromHeader(Name = "X-Tenant-Id")] string? tenantId,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromQuery] bool enable,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(apiKey))
        {
            return BadRequest(new
            {
                error = "Missing X-Tenant-Id or X-Api-Key header."
            });
        }

        await _tenantConfigRepository.SetPersonalizationAsync(tenantId, apiKey, enable, ct);

        return Ok();
    }
}
