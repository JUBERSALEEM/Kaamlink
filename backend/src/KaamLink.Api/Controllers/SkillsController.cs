using KaamLink.Application.Skills;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaamLink.Api.Controllers;

/// <summary>Seeded occupation catalog used by onboarding (authenticated clients).</summary>
[ApiController]
[Authorize]
[Route("api/v1/skills")]
public sealed class SkillsController(ISkillCatalogService skillCatalog) : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<ActionResult<SkillCatalogResponse>> GetCatalog(CancellationToken cancellationToken) =>
        Ok(await skillCatalog.GetCatalogAsync(cancellationToken));
}
