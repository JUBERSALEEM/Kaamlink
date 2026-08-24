using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaamLink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase05Assessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "assessment_verified_at",
                table: "worker_skills",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "skill_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pass_mark_percent = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_assessments", x => x.id);
                    table.ForeignKey(
                        name: "FK_skill_assessments_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assessment_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_questions = table.Column<int>(type: "integer", nullable: false),
                    correct_answers = table.Column<int>(type: "integer", nullable: true),
                    score_percent = table.Column<int>(type: "integer", nullable: true),
                    passed = table.Column<bool>(type: "boolean", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_assessment_attempts_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assessment_attempts_worker_profiles_worker_profile_id",
                        column: x => x.worker_profile_id,
                        principalTable: "worker_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assessment_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    text_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    option_a_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    option_a_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    option_b_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    option_b_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    option_c_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    option_c_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    option_d_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    option_d_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    correct_option_index = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_assessment_questions_skill_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "skill_assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assessment_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    selected_option_index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_assessment_answers_assessment_attempts_attempt_id",
                        column: x => x.attempt_id,
                        principalTable: "assessment_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assessment_answers_assessment_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "assessment_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assessment_answers_question_id",
                table: "assessment_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ux_assessment_answers_attempt_question",
                table: "assessment_answers",
                columns: new[] { "attempt_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assessment_attempts_skill_id",
                table: "assessment_attempts",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_attempts_worker_profile_id",
                table: "assessment_attempts",
                column: "worker_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_questions_assessment_id",
                table: "assessment_questions",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ux_skill_assessments_skill_id",
                table: "skill_assessments",
                column: "skill_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "assessment_answers");
            migrationBuilder.DropTable(name: "assessment_attempts");
            migrationBuilder.DropTable(name: "assessment_questions");
            migrationBuilder.DropTable(name: "skill_assessments");

            migrationBuilder.DropColumn(
                name: "assessment_verified_at",
                table: "worker_skills");
        }
    }
}
