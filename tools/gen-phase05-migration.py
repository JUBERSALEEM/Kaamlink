#!/usr/bin/env python3
"""One-shot generator (kept for provenance): applies the Phase 05 model changes to
AppDbContextModelSnapshot.cs and generates the Phase05Assessments designer file."""
import re

SNAP = "backend/src/KaamLink.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs"
DESIGNER = "backend/src/KaamLink.Infrastructure/Persistence/Migrations/20260824150000_Phase05Assessments.Designer.cs"

with open(SNAP, encoding="utf-8") as f:
    text = f.read()


def snake(name):
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).lower()


# ---- 1) worker_skills: assessment_verified_at into the first-pass properties block
anchor = ('.HasColumnName("verification_state");\n'
          '\n'
          '                    b.Property<int>("YearsExperience")')
assert text.count(anchor) == 1, f"worker_skills anchor count: {text.count(anchor)}"
text = text.replace(
    anchor,
    '.HasColumnName("verification_state");'
    + '\n\n                    b.Property<DateTimeOffset?>("AssessmentVerifiedAt")'
    + '\n                        .HasColumnType("timestamp with time zone")'
    + '\n                        .HasColumnName("assessment_verified_at");'
    + '\n\n                    b.Property<int>("YearsExperience")',
)


def props(entity, body, table):
    return (
        f'            modelBuilder.Entity("{entity}", b =>\n                {{\n'
        + "\n".join(body)
        + f'\n                    b.ToTable("{table}");\n                }});\n'
    )


def idpk():
    return ('                    b.Property<Guid>("Id")\n'
            '                        .ValueGeneratedOnAdd()\n'
            '                        .HasColumnType("uuid")\n'
            '                        .HasColumnName("id");\n\n'
            '                    b.HasKey("Id");\n')


def prop(clr, name, coltype):
    return (f'                    b.Property<{clr}>("{name}")\n'
            f'                        .HasColumnType("{coltype}")\n'
            f'                        .HasColumnName("{snake(name)}");\n')


def proptxt(name, maxlen):
    return (f'                    b.Property<string>("{name}")\n'
            '                        .IsRequired()\n'
            f'                        .HasMaxLength({maxlen})\n'
            f'                        .HasColumnType("character varying({maxlen})")\n'
            f'                        .HasColumnName("{snake(name)}");\n')


def ix(cols, name, unique=False):
    colsig = ", ".join(f'"{c}"' for c in cols)
    s = f'                    b.HasIndex({colsig})\n'
    if unique:
        s += '                        .IsUnique()\n'
    return s + f'                        .HasDatabaseName("{name}");\n'


g = lambda n: prop("Guid", n, "uuid")
i = lambda n, nn=False: prop("int?" if nn else "int", n, "integer")
b_ = lambda n, nn=False: prop("bool?" if nn else "bool", n, "boolean")
ts = lambda n, nn=False: prop("DateTimeOffset?" if nn else "DateTimeOffset", n, "timestamp with time zone")

skill_assessment = props(
    "KaamLink.Domain.Assessments.SkillAssessment",
    [idpk(), ts("CreatedAt"), b_("IsActive"), i("PassMarkPercent"), g("SkillId"),
     ix(["SkillId"], "ux_skill_assessments_skill_id", unique=True)],
    "skill_assessments")

assessment_question = props(
    "KaamLink.Domain.Assessments.AssessmentQuestion",
    [idpk(), g("AssessmentId"), i("CorrectOptionIndex"), proptxt("OptionAAr", 200), proptxt("OptionAEn", 200),
     proptxt("OptionBAr", 200), proptxt("OptionBEn", 200), proptxt("OptionCAr", 200), proptxt("OptionCEn", 200),
     proptxt("OptionDAr", 200), proptxt("OptionDEn", 200), i("SortOrder"), proptxt("TextAr", 300), proptxt("TextEn", 300),
     ix(["AssessmentId"], "ix_assessment_questions_assessment_id")],
    "assessment_questions")

assessment_attempt = props(
    "KaamLink.Domain.Assessments.AssessmentAttempt",
    [idpk(), ts("CompletedAt", nn=True), i("CorrectAnswers", nn=True), b_("Passed", nn=True),
     i("ScorePercent", nn=True), g("SkillId"), ts("StartedAt"), i("TotalQuestions"), g("WorkerProfileId"),
     ix(["SkillId"], "ix_assessment_attempts_skill_id"),
     ix(["WorkerProfileId"], "ix_assessment_attempts_worker_profile_id")],
    "assessment_attempts")

assessment_answer = props(
    "KaamLink.Domain.Assessments.AssessmentAnswer",
    [idpk(), g("AttemptId"), g("QuestionId"), i("SelectedOptionIndex"),
     ix(["QuestionId"], "ix_assessment_answers_question_id"),
     ix(["AttemptId", "QuestionId"], "ux_assessment_answers_attempt_question", unique=True)],
    "assessment_answers")

props_blocks = ("\n" + skill_assessment + "\n" + assessment_question + "\n"
                + assessment_attempt + "\n" + assessment_answer + "\n")

tail_marker = ('            modelBuilder.Entity("KaamLink.Domain.Companies.CompanyMember", b =>\n'
               '                {\n'
               '                    b.HasOne(')
assert text.count(tail_marker) == 1

# ---- 2) relationship tail blocks (AssessmentAnswer first: it creates "Answers")
def rel(entity, body):
    return f'            modelBuilder.Entity("{entity}", b =>\n                {{\n{body}\n                }});\n\n'


def fk(principal, nav_ref, with_many, column, delete):
    nav = f', {nav_ref}' if nav_ref else ", null"
    wm = f'("{with_many}")' if with_many else "()"
    return (f'                    b.HasOne("{principal}"{nav})\n'
            f'                        .WithMany{wm}\n'
            f'                        .HasForeignKey("{column}")\n'
            f'                        .OnDelete(DeleteBehavior.{delete})\n'
            '                        .IsRequired();\n')


rel_answer = rel(
    "KaamLink.Domain.Assessments.AssessmentAnswer",
    fk("KaamLink.Domain.Assessments.AssessmentAttempt", '"Attempt"', "Answers", "AttemptId", "Cascade") + "\n"
    + fk("KaamLink.Domain.Assessments.AssessmentQuestion", None, None, "QuestionId", "Restrict") + "\n"
    + '                    b.Navigation("Attempt");')

rel_attempt = rel(
    "KaamLink.Domain.Assessments.AssessmentAttempt",
    fk("KaamLink.Domain.Skills.Skill", None, None, "SkillId", "Restrict") + "\n"
    + fk("KaamLink.Domain.Workers.WorkerProfile", None, None, "WorkerProfileId", "Cascade") + "\n"
    + '                    b.Navigation("Answers");')

rel_question = rel(
    "KaamLink.Domain.Assessments.AssessmentQuestion",
    fk("KaamLink.Domain.Assessments.SkillAssessment", '"Assessment"', None, "AssessmentId", "Cascade") + "\n"
    + '                    b.Navigation("Assessment");')

rel_assessment = rel(
    "KaamLink.Domain.Assessments.SkillAssessment",
    fk("KaamLink.Domain.Skills.Skill", '"Skill"', None, "SkillId", "Cascade") + "\n"
    + '                    b.Navigation("Skill");')

text = text.replace(
    tail_marker,
    props_blocks + rel_answer + rel_attempt + rel_question + rel_assessment + "\n" + tail_marker)

with open(SNAP, "w", encoding="utf-8") as f:
    f.write(text)

# ---- 3) designer from the updated snapshot
designer = text.replace(
    "using Microsoft.EntityFrameworkCore.Infrastructure;\nusing Microsoft.EntityFrameworkCore.Storage.ValueConversion;",
    "using Microsoft.EntityFrameworkCore.Infrastructure;\nusing Microsoft.EntityFrameworkCore.Migrations;\nusing Microsoft.EntityFrameworkCore.Storage.ValueConversion;")
designer = designer.replace(
    '    [DbContext(typeof(AppDbContext))]\n'
    '    partial class AppDbContextModelSnapshot : ModelSnapshot\n'
    '    {\n'
    '        protected override void BuildModel(ModelBuilder modelBuilder)',
    '    [DbContext(typeof(AppDbContext))]\n'
    '    [Migration("20260824150000_Phase05Assessments")]\n'
    '    partial class Phase05Assessments\n'
    '    {\n'
    '        protected override void BuildTargetModel(ModelBuilder modelBuilder)')
assert '[Migration("20260824150000_Phase05Assessments")]' in designer
assert designer.count("[DbContext(typeof(AppDbContext))]") == 1

with open(DESIGNER, "w", encoding="utf-8") as f:
    f.write(designer)

# ---- 4) self-check: navigation lookups must have a creator on an earlier line
for label, content in (("snapshot", text), ("designer", designer)):
    lines = content.splitlines()
    bad = []
    for idx, line in enumerate(lines):
        m = re.search(r'b\.Navigation\("(\w+)"\)', line)
        if not m:
            continue
        nav = m.group(1)
        prior = "\n".join(lines[:idx])
        created = re.search(
            r'(WithMany|WithOne)\("' + re.escape(nav) + r'"\)' + r'|Has(One|Many)\("[^"]+",\s*"' + re.escape(nav) + r'"\)',
            prior)
        if not created:
            bad.append(f"{label}:{idx + 1} {nav}")
    assert not bad, bad

print("OK: snapshot updated, designer generated, ordering self-check passed")
