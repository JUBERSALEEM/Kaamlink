using System.Globalization;
using KaamLink.Core.Api;
using KaamLink.Resources.Strings;

namespace KaamLink.Core.Presentation
{
    /// <summary>True when the device UI culture is Arabic (drives EN/AR API field selection).</summary>
    public static class LocalizationHelpers
    {
        public static bool IsArabic =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

        public static string Localized(string nameEn, string nameAr) => IsArabic ? nameAr : nameEn;

        public static string LocalizedSkill(SkillItemDto skill) => Localized(skill.NameEn, skill.NameAr);

        public static string LocalizedSkill(WorkerSkillItemDto skill) => Localized(skill.NameEn, skill.NameAr);

        public static string LocalizedSkill(VacancySkillItemDto skill) => Localized(skill.NameEn, skill.NameAr);
    }

    /// <summary>A pickable option: wire value (backend enum name) + localized label.</summary>
    public sealed record EnumOption(string Value, string Label)
    {
        public override string ToString() => Label;
    }

    /// <summary>
    /// Central mapping between backend enum names and localized labels.
    /// Enum names are part of the API contract (never localized on the wire);
    /// labels come from AppStrings only — no magic strings in views/view models.
    /// </summary>
    public static class EnumDisplay
    {
        // Mirrors backend/src/KaamLink.Domain/Shared/Emirate.cs
        public static readonly IReadOnlyList<EnumOption> Emirates =
        [
            new("AbuDhabi", AppStrings.EmirateAbuDhabi),
            new("Dubai", AppStrings.EmirateDubai),
            new("Sharjah", AppStrings.EmirateSharjah),
            new("Ajman", AppStrings.EmirateAjman),
            new("UmmAlQuwain", AppStrings.EmirateUmmAlQuwain),
            new("RasAlKhaimah", AppStrings.EmirateRasAlKhaimah),
            new("Fujairah", AppStrings.EmirateFujairah),
        ];

        // Mirrors backend/src/KaamLink.Domain/Workers/Availability.cs
        public static readonly IReadOnlyList<EnumOption> Availabilities =
        [
            new("Immediately", AppStrings.AvailabilityImmediately),
            new("WithinTwoWeeks", AppStrings.AvailabilityWithinTwoWeeks),
            new("WithinOneMonth", AppStrings.AvailabilityWithinOneMonth),
            new("NotAvailable", AppStrings.AvailabilityNotAvailable),
        ];

        // Mirrors backend/src/KaamLink.Domain/Companies/BusinessCategory.cs
        public static readonly IReadOnlyList<EnumOption> BusinessCategories =
        [
            new("ConstructionContracting", AppStrings.BizConstructionContracting),
            new("ManufacturingFactory", AppStrings.BizManufacturingFactory),
            new("Workshop", AppStrings.BizWorkshop),
            new("LogisticsWarehouse", AppStrings.BizLogisticsWarehouse),
            new("FacilityServices", AppStrings.BizFacilityServices),
            new("Other", AppStrings.BizOther),
        ];

        public static string Emirate(string? value) => Lookup(Emirates, value);

        public static string Availability(string? value) => Lookup(Availabilities, value);

        public static string BusinessCategory(string? value) => Lookup(BusinessCategories, value);

        // Mirrors backend/src/KaamLink.Domain/Companies/CompanyRole.cs
        public static string CompanyRole(string? value) => value switch
        {
            "Owner" => AppStrings.CompanyRoleOwner,
            "Admin" => AppStrings.CompanyRoleAdmin,
            "HiringManager" => AppStrings.CompanyRoleHiringManager,
            "Supervisor" => AppStrings.CompanyRoleSupervisor,
            "Viewer" => AppStrings.CompanyRoleViewer,
            _ => value ?? string.Empty
        };

        // Mirrors backend/src/KaamLink.Domain/Companies/CompanyVerificationState.cs
        public static string VerificationState(string? value) => value switch
        {
            "Pending" => AppStrings.VerificationPending,
            "Verified" => AppStrings.VerificationVerified,
            "Rejected" => AppStrings.VerificationRejected,
            _ => value ?? string.Empty
        };

        // Mirrors backend/src/KaamLink.Domain/Jobs/VacancyStatus.cs
        public static string VacancyStatus(string? value) => value switch
        {
            "Draft" => AppStrings.VacancyStatusDraft,
            "Active" => AppStrings.VacancyStatusActive,
            "Paused" => AppStrings.VacancyStatusPaused,
            "Closed" => AppStrings.VacancyStatusClosed,
            _ => value ?? string.Empty
        };

        // Mirrors backend/src/KaamLink.Domain/Jobs/ApplicationStatus.cs
        public static string ApplicationStatus(string? value) => value switch
        {
            "Applied" => AppStrings.ApplicationStatusApplied,
            "Viewed" => AppStrings.ApplicationStatusViewed,
            "Shortlisted" => AppStrings.ApplicationStatusShortlisted,
            "Rejected" => AppStrings.ApplicationStatusRejected,
            "Hired" => AppStrings.ApplicationStatusHired,
            "Withdrawn" => AppStrings.ApplicationStatusWithdrawn,
            _ => value ?? string.Empty
        };

        private static string Lookup(IReadOnlyList<EnumOption> options, string? value) =>
            value is null
                ? string.Empty
                : options.FirstOrDefault(o => o.Value == value)?.Label ?? value;
    }
}
