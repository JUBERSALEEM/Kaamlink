using KaamLink.Domain.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KaamLink.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotent seed of the occupation catalog (curated UAE-practical trades).
/// GUIDs are fixed so references stay stable across environments.
///
/// IMPORTANT (Master Prompt §1/§L): RequiresCertification is set only where a licence
/// is unambiguous (vehicle operation). All other entries default to false pending
/// admin curation with professional legal review — we never invent UAE requirements.
/// </summary>
public static class SkillCatalogSeeder
{
    private static readonly (Guid Id, string En, string Ar)[] Categories =
    [
        (Guid.Parse("5887c5ec-086e-4e5a-a9f3-933c926eeab6"), "Construction & Site", "الإنشاءات والمواقع"),
        (Guid.Parse("298ab6db-4f17-4b0c-99b9-82c15b77241e"), "Fabrication & Welding", "التصنيع واللحام"),
        (Guid.Parse("4ac433f0-e055-4c18-bfd0-1bc1d3de6b5b"), "Electrical & Technical", "الكهرباء والأعمال الفنية"),
        (Guid.Parse("c2b5c52d-5779-4cdc-8f42-bb12cec5ce61"), "Mechanical & Maintenance", "الميكانيكا والصيانة"),
        (Guid.Parse("a01c2d3c-f7f7-4caf-b380-d9fc947df0df"), "Warehouse & Logistics", "المستودعات واللوجستيات"),
        (Guid.Parse("1aac2b7d-e194-4d71-983c-9c3adbe5540a"), "Finishing & Interior", "التشطيبات والديكور"),
    ];

    private static readonly (Guid Id, string CategoryEn, string En, string Ar, bool RequiresCertification)[] Skills =
    [
        (Guid.Parse("93f154a4-1dfa-4128-ac72-e402a8cb969b"), "Construction & Site", "Mason", "بنّاء", false),
        (Guid.Parse("be0961df-6652-48eb-a855-2d2f89ece092"), "Construction & Site", "Steel Fixer", "حداد تسليح", false),
        (Guid.Parse("009780a5-0fa2-422f-9e73-51bc24d669bc"), "Construction & Site", "Scaffolder", "عامل سقالات", false),
        (Guid.Parse("27e2e1b8-cca2-4117-a3c6-62fef7c46744"), "Construction & Site", "General Construction Helper", "عامل إنشاءات عام", false),
        (Guid.Parse("bad55086-6583-4178-9d7c-47f476917eec"), "Fabrication & Welding", "Aluminium Fabricator", "فني ألمنيوم", false),
        (Guid.Parse("61b1b0ff-b757-46f5-a680-c96812bc0acb"), "Fabrication & Welding", "Welder", "لحّام", false),
        (Guid.Parse("8aec57c7-ad98-4b8d-885d-5b455f452567"), "Fabrication & Welding", "Steel Fabricator", "فني تصنيع حديد", false),
        (Guid.Parse("f3736e43-fa1a-4f4f-bbe9-af9ffd7985cc"), "Electrical & Technical", "Electrician", "كهربائي", false),
        (Guid.Parse("58a3815c-ba53-42b8-9800-62b8862efa15"), "Electrical & Technical", "AC Technician", "فني تكييف", false),
        (Guid.Parse("b49da54e-39e3-4f1e-968b-8ed7a5304d43"), "Electrical & Technical", "Plumber", "سباك", false),
        (Guid.Parse("70d11ff1-b686-4f25-a0d9-e03b6277cf5b"), "Electrical & Technical", "Machine Operator", "مشغل آلة", false),
        (Guid.Parse("52a8f1aa-ce65-400d-91ec-a27e0c30cccb"), "Mechanical & Maintenance", "Mechanical Helper", "مساعد ميكانيكي", false),
        (Guid.Parse("578fba1c-d762-4f48-8366-c06466260dad"), "Mechanical & Maintenance", "Maintenance Technician", "فني صيانة", false),
        (Guid.Parse("57aea889-831b-4ecf-8395-f87e01bf4405"), "Warehouse & Logistics", "Warehouse Helper", "عامل مستودع", false),
        (Guid.Parse("6cdfc9a5-44a1-4b23-84be-ff884da8490d"), "Warehouse & Logistics", "Forklift Operator", "مشغل رافعة شوكية", false),
        (Guid.Parse("ce4dccee-4687-4af8-8937-f84b21a3c449"), "Warehouse & Logistics", "Light Vehicle Driver", "سائق مركبة خفيفة", true),
        (Guid.Parse("531f12d8-448c-4b77-8986-06c1fe99fff0"), "Warehouse & Logistics", "Packaging Helper", "عامل تعبئة", false),
        (Guid.Parse("5fa9de7d-0922-4687-86d2-2cf67f3a0049"), "Finishing & Interior", "Painter", "دهّان", false),
        (Guid.Parse("e172fea9-0cbd-4034-b963-4047e49e0daa"), "Finishing & Interior", "Carpenter", "نجّار", false),
        (Guid.Parse("51613a8a-de33-4ee7-9600-39aea79bc167"), "Finishing & Interior", "Tile & Marble Mason", "عامل بلاط ورخام", false),
    ];

    public static async Task EnsureSeededAsync(AppDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        var existingCategoryIds = await db.SkillCategories.Select(c => c.Id).ToListAsync(ct);
        var missingCategories = Categories
            .Where(c => !existingCategoryIds.Contains(c.Id))
            .Select((c, index) => SkillCategory.Create(c.Id, c.En, c.Ar, index + 1))
            .ToList();

        if (missingCategories.Count > 0)
            await db.SkillCategories.AddRangeAsync(missingCategories, ct);

        var categoryIdByEn = Categories.ToDictionary(c => c.En, c => c.Id);

        var existingSkillIds = await db.Skills.Select(s => s.Id).ToListAsync(ct);
        var missingSkillsByCategory = Skills
            .Where(s => !existingSkillIds.Contains(s.Id))
            .GroupBy(s => s.CategoryEn)
            .ToList();

        foreach (var group in missingSkillsByCategory)
        {
            var sort = 0;
            var toAdd = group
                .Select(s => Skill.Create(
                    s.Id,
                    categoryIdByEn[s.CategoryEn],
                    s.En,
                    s.Ar,
                    s.RequiresCertification,
                    ++sort))
                .ToList();
            await db.Skills.AddRangeAsync(toAdd, ct);
        }

        if (missingCategories.Count > 0 || missingSkillsByCategory.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger?.LogInformation(
                "Skill catalog seeded: {Categories} categories, {Skills} skills added.",
                missingCategories.Count,
                missingSkillsByCategory.Sum(g => g.Count()));
        }
    }
}
