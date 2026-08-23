namespace KaamLink.Application.Skills;

public interface ISkillCatalogService
{
    /// <summary>Seeded occupation catalog used by onboarding and job discovery.</summary>
    Task<SkillCatalogResponse> GetCatalogAsync(CancellationToken ct = default);
}
