using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaamLink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase04Jobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vacancies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    emirate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    salary_min_aed = table.Column<int>(type: "integer", nullable: true),
                    salary_max_aed = table.Column<int>(type: "integer", nullable: true),
                    headcount = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacancies", x => x.id);
                    table.ForeignKey(
                        name: "FK_vacancies_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vacancies_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vacancy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cover_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_applications", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_applications_vacancies_vacancy_id",
                        column: x => x.vacancy_id,
                        principalTable: "vacancies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_applications_worker_profiles_worker_profile_id",
                        column: x => x.worker_profile_id,
                        principalTable: "worker_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vacancy_skills",
                columns: table => new
                {
                    vacancy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacancy_skills", x => new { x.vacancy_id, x.skill_id });
                    table.ForeignKey(
                        name: "FK_vacancy_skills_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vacancy_skills_vacancies_vacancy_id",
                        column: x => x.vacancy_id,
                        principalTable: "vacancies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_applications_worker_profile_id",
                table: "job_applications",
                column: "worker_profile_id");

            migrationBuilder.CreateIndex(
                name: "ux_job_applications_vacancy_worker",
                table: "job_applications",
                columns: new[] { "vacancy_id", "worker_profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vacancies_company_id",
                table: "vacancies",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacancies_created_by_user_id",
                table: "vacancies",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacancies_status_created_at",
                table: "vacancies",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_vacancy_skills_skill_id",
                table: "vacancy_skills",
                column: "skill_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_applications");

            migrationBuilder.DropTable(
                name: "vacancy_skills");

            migrationBuilder.DropTable(
                name: "vacancies");
        }
    }
}
