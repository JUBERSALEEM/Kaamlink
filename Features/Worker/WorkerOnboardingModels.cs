using KaamLink.Core.Api;
using KaamLink.Core.Presentation;
using KaamLink.Resources.Strings;

namespace KaamLink.Features.Worker
{
    /// <summary>A catalog category rendered localized in a picker.</summary>
    public sealed record SkillCategoryOption(SkillCategoryDto Category, string Label)
    {
        public override string ToString() => Label;
    }

    /// <summary>A catalog skill rendered localized in a picker.</summary>
    public sealed record SkillOption(SkillItemDto Skill, string Label)
    {
        public override string ToString() => Label;
    }

    /// <summary>
    /// A skill the worker added during onboarding (self-declared years).
    /// Display text is precomputed so the row template stays trivial.
    /// </summary>
    public sealed record AddedSkillModel(Guid SkillId, string Label, int Years)
    {
        public string Summary => $"{Label} · {Years} {AppStrings.YearsShort}";

        public static AddedSkillModel FromOption(SkillOption option, int years) =>
            new(option.Skill.Id, LocalizationHelpers.LocalizedSkill(option.Skill), years);
    }
}
