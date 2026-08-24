using KaamLink.Domain.Jobs;
using KaamLink.Domain.Shared;

namespace KaamLink.Application.Abstractions;

public interface IVacancyRepository
{
    Task AddAsync(Vacancy vacancy, CancellationToken ct = default);

    /// <summary>Tracked entity for write flows (edit/status). No navigation includes.</summary>
    Task<Vacancy?> FindByIdAsync(Guid vacancyId, CancellationToken ct = default);

    /// <summary>
    /// Worker feed: ACTIVE vacancies only, newest first, filters ANDed.
    /// <paramref name="take"/> may be requested one higher than the page size
    /// (caller computes HasMore from the surplus row).
    /// </summary>
    Task<IReadOnlyList<Vacancy>> SearchActiveAsync(
        Emirate? emirate, Guid? skillId, string? keyword, int skip, int take, CancellationToken ct = default);

    /// <summary>Company management list (any status), newest first.</summary>
    Task<IReadOnlyList<Vacancy>> ListByCompanyAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>Full replace of the vacancy's skill list (idempotent per call).</summary>
    Task ReplaceSkillsAsync(Guid vacancyId, IReadOnlyList<VacancySkill> skills, CancellationToken ct = default);

    Task<IReadOnlyList<VacancySkill>> ListSkillsAsync(Guid vacancyId, CancellationToken ct = default);
}
