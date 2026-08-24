using KaamLink.Domain.Assessments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KaamLink.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotent seed of skill assessment templates + question banks (Phase 05).
/// Fixed GUIDs keep rows stable across environments; the whole seed is a no-op
/// once any template exists.
///
/// Content rules (Master Prompt): questions are practical trade knowledge only.
/// Driver questions deliberately avoid specific UAE legal figures (fines / black
/// points / exact limits) — those require professional legal review before we
/// assert them as facts.
/// </summary>
public static class AssessmentCatalogSeeder
{
    private static readonly DateTimeOffset SeededAt = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    // Skills these templates attach to (ids mirror SkillCatalogSeeder).
    private static readonly Guid WelderSkillId = Guid.Parse("61b1b0ff-b757-46f5-a680-c96812bc0acb");
    private static readonly Guid AluminiumFabricatorSkillId = Guid.Parse("bad55086-6583-4178-9d7c-47f476917eec");
    private static readonly Guid LightVehicleDriverSkillId = Guid.Parse("ce4dccee-4687-4af8-8937-f84b21a3c449");

    private static readonly Guid WelderTemplateId = Guid.Parse("b5e5e000-0000-4000-8000-000000000001");
    private static readonly Guid AluminiumTemplateId = Guid.Parse("b5e5e000-0000-4000-8000-000000000002");
    private static readonly Guid DriverTemplateId = Guid.Parse("b5e5e000-0000-4000-8000-000000000003");

    private const int PassMarkPercent = 60;

    public static async Task EnsureSeededAsync(AppDbContext db, ILogger logger)
    {
        if (await db.SkillAssessments.AnyAsync())
        {
            logger.LogDebug("Assessment catalog already present; skipping seed.");
            return;
        }

        db.SkillAssessments.AddRange(
            SkillAssessment.Create(WelderTemplateId, WelderSkillId, PassMarkPercent, SeededAt),
            SkillAssessment.Create(AluminiumTemplateId, AluminiumFabricatorSkillId, PassMarkPercent, SeededAt),
            SkillAssessment.Create(DriverTemplateId, LightVehicleDriverSkillId, PassMarkPercent, SeededAt));

        var order = 0;
        foreach (var question in WelderQuestions())
            db.AssessmentQuestions.Add(Q(WelderTemplateId, ++order, question));
        order = 0;
        foreach (var question in AluminiumQuestions())
            db.AssessmentQuestions.Add(Q(AluminiumTemplateId, ++order, question));
        order = 0;
        foreach (var question in DriverQuestions())
            db.AssessmentQuestions.Add(Q(DriverTemplateId, ++order, question));

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded assessment catalog: 3 templates, 15 questions.");
    }

    private sealed record QuestionSeed(
        string TextEn, string TextAr,
        string AEn, string AAr, string BEn, string BAr,
        string CEn, string CAr, string DEn, string DAr,
        int Correct);

    private static AssessmentQuestion Q(Guid assessmentId, int sortOrder, QuestionSeed seed) =>
        AssessmentQuestion.Create(
            Guid.NewGuid(), assessmentId, seed.TextEn, seed.TextAr,
            seed.AEn, seed.AAr, seed.BEn, seed.BAr, seed.CEn, seed.CAr, seed.DEn, seed.DAr,
            seed.Correct, sortOrder);

    private static QuestionSeed[] WelderQuestions() =>
    [
        new(
            "In MMA (stick) welding, what is the main job of the electrode's flux coating?",
            "في لحام القوس اليدوي (MMA)، ما الوظيفة الرئيسية لطلاء الفلوكس على القطب؟",
            "Cool the electrode", "تبريد القطب",
            "Shield the molten metal from air and stabilize the arc", "حماية المعدن المنصهر من الهواء وتثبيت القوس",
            "Colour the weld", "تلوين اللحام",
            "Make the electrode stiffer", "جعل القطب أكثر صلابة",
            1),
        new(
            "Before striking an arc, which PPE must a welder ALWAYS wear?",
            "قبل بدء القوس، ما معدات الحماية الشخصية التي يجب على اللحّام ارتداؤها دائمًا؟",
            "Welding helmet with the correct shade, gloves and fire-resistant clothing", "خوذة لحام بالدرجة المناسبة وقفازات وملابس مقاومة للحريق",
            "Only sunglasses", "نظارة شمسية فقط",
            "Ear plugs only", "سدادات أذن فقط",
            "A dust mask only", "كمامة غبار فقط",
            0),
        new(
            "Why must gas cylinders (oxygen / acetylene / argon) be secured upright?",
            "لماذا يجب تثبيت أسطوانات الغاز (أكسجين/أسيتيلين/أرجون) في وضع رأسي؟",
            "They take less floor space", "لتشغل مساحة أقل",
            "So labels stay readable", "لتبقى الملصقات مقروءة",
            "To prevent a fall that could shear the valve and release gas uncontrollably", "لمنع سقوطها وقطع الصمام وتسرّب الغاز بشكل غير مسيطر عليه",
            "They only work upright", "لأنها لا تعمل إلا وهي رأسية",
            2),
        new(
            "You see strong porosity (pin holes) in a finished weld bead. The most common cause is:",
            "لاحظت مسامية قوية (ثقوب دقيقة) في خط اللحام النهائي. السبب الأكثر شيوعًا هو:",
            "Travel speed too slow", "سرعة تحرك بطيئة جدًا",
            "Contamination or moisture: dirty metal, damp electrodes, or drafts disturbing the shielding", "تلوث أو رطوبة: معدن غير نظيف أو أقطاب رطبة أو تيارات هواء تُخل بالحماية",
            "Weld ran too hot", "اللحام كان حارًا جدًا",
            "Wrong electrode colour", "لون قطب خاطئ",
            1),
        new(
            "Before welding a container that previously held fuel or chemicals, you must:",
            "قبل لحام حاوية كانت تحتوي سابقًا على وقود أو مواد كيميائية، يجب عليك:",
            "Weld quickly to avoid fumes", "اللحام بسرعة لتجنب الأبخرة",
            "Only weld it outdoors", "لحامها في الخارج فقط",
            "Ask a colleague to watch", "طلب مراقبة زميل",
            "Clean and purge it properly and confirm it is gas-free first", "تنظيفها وتطهيرها جيدًا والتأكد من خلوها من الغازات أولًا",
            3),
    ];

    private static QuestionSeed[] AluminiumQuestions() =>
    [
        new(
            "Which shielding gas is normally used for TIG welding aluminium?",
            "ما الغاز الواقي المستخدم عادةً في لحام الألمنيوم بتقنية TIG؟",
            "Carbon dioxide", "ثاني أكسيد الكربون",
            "Argon", "الأرجون",
            "Oxygen", "الأكسجين",
            "Nitrogen", "النيتروجين",
            1),
        new(
            "Why is AC (alternating current) preferred when TIG welding aluminium?",
            "لماذا يُفضّل التيار المتردد (AC) عند لحام الألمنيوم بتقنية TIG؟",
            "It breaks the surface oxide layer while still penetrating the metal", "لأنه يكسر طبقة الأكسيد السطحية مع استمرار اختراق المعدن",
            "It is cheaper to run", "لأنه أرخص في التشغيل",
            "It uses less gas", "لأنه يستهلك غازًا أقل",
            "It makes the weld shinier", "لأنه يجعل اللحام أكثر لمعانًا",
            0),
        new(
            "Before welding aluminium, the joint area should be brushed with:",
            "قبل لحام الألمنيوم، يجب تنظيف منطقة الوصلة بفرشاة:",
            "A dedicated stainless-steel wire brush used only for aluminium", "سلكية من الستانلس ستيل مخصصة للألمنيوم فقط",
            "The same brush used for mild steel", "نفس الفرشاة المستخدمة للحديد العادي",
            "An oily rag", "قطعة قماش مدهونة بالزيت",
            "A copper brush", "نحاسية",
            0),
        new(
            "Aluminium sections are usually cut with:",
            "تُقطع مقاطع الألمنيوم عادةً باستخدام:",
            "An oxy-fuel torch", "شعلة أوكسي-وقود",
            "A wood chisel", "إزميل خشب",
            "A carbide-tipped saw or suitable disc, with deburring afterwards", "منشار بأسنان كربيد أو قرص مناسب مع إزالة الزوائد بعد القطع",
            "A hammer only", "مطرقة فقط",
            2),
        new(
            "Anodizing aluminium is done to:",
            "يتم أنودة الألمنيوم من أجل:",
            "Make it magnetic", "جعله مغناطيسيًا",
            "Build a hard protective oxide layer and allow decorative coloring", "تكوين طبقة أكسيد صلبة واقية وإتاحة التلوين الزخرفي",
            "Make it heavier", "زيادة وزنه",
            "Melt its surface", "صهر سطحه",
            1),
    ];

    private static QuestionSeed[] DriverQuestions() =>
    [
        new(
            "Before moving off, a professional driver should always:",
            "قبل الانطلاق، يجب على السائق المحترف دائمًا:",
            "Adjust seat and mirrors, fasten the seat belt and check blind spots", "ضبط المقعد والمرايا وربط حزام الأمان وفحص النقاط العمياء",
            "Turn on loud music", "تشغيل موسيقى عالية",
            "Check the phone for messages", "تفقد الهاتف للرسائل",
            "Rev the engine hard", "زيادة دوران المحرك بقوة",
            0),
        new(
            "The safest way to manage following distance is to:",
            "الطريقة الأكثر أمانًا لإدارة مسافة التتبع هي:",
            "Copy the speed of the vehicle ahead", "تقليد سرعة المركبة الأمامية",
            "Keep a time gap that grows in rain, fog or at night", "الحفاظ على فجوة زمنية تزداد في المطر أو الضباب أو الليل",
            "Stay very close so no one can overtake", "البقاء قريبًا جدًا حتى لا يتجاوزك أحد",
            "Follow large trucks closely", "تتبع الشاحنات الكبيرة عن قرب",
            1),
        new(
            "If the vehicle starts to skid on a wet road, you should:",
            "إذا بدأت المركبة في الانزلاق على طريق مبلل، يجب عليك:",
            "Brake as hard as possible immediately", "الفرملة بأقصى قوة فورًا",
            "Accelerate out of the skid", "التسارع للخروج من الانزلاق",
            "Stay calm, ease off the accelerator and steer gently where you want to go", "التزام الهدوء وتخفيف الضغط على دواسة الوقود والتوجيه بلطف إلى الاتجاه المطلوب",
            "Pull the handbrake", "سحب فرامل اليد",
            2),
        new(
            "Tyre pressure should be checked:",
            "يجب فحص ضغط الإطارات:",
            "Only when a tyre looks flat", "فقط عندما يبدو الإطار فارغًا",
            "Once a year", "مرة في السنة",
            "Never — modern tyres need no checks", "أبدًا — الإطارات الحديثة لا تحتاج فحصًا",
            "Regularly and before long trips, while the tyres are cold", "بانتظام وقبل الرحلات الطويلة والإطارات باردة",
            3),
        new(
            "Using a handheld mobile phone while driving is:",
            "استخدام الهاتف المحمول باليد أثناء القيادة هو:",
            "Fine when traffic is slow", "مقبول عندما تكون الحركة بطيئة",
            "Dangerous and prohibited — attention must stay on the road at all times", "خطير ومحظور — يجب أن يبقى الانتباه على الطريق في جميع الأوقات",
            "Allowed for experienced drivers", "مسموح للسائقين ذوي الخبرة",
            "Allowed on straight roads", "مسموح في الطرق المستقيمة",
            1),
    ];
}
