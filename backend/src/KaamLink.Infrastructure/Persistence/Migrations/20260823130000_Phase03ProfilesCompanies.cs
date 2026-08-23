using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaamLink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase03ProfilesCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    emirate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    trade_license_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    verification_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    verification_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "skill_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_en = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "company_members",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_members", x => new { x.company_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_company_members_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_en = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    requires_certification = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.id);
                    table.ForeignKey(
                        name: "FK_skills_skill_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "skill_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "worker_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    primary_skill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    emirate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    availability = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    expected_salary_min_aed = table.Column<int>(type: "integer", nullable: true),
                    expected_salary_max_aed = table.Column<int>(type: "integer", nullable: true),
                    about = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    profile_completeness_pct = table.Column<int>(type: "integer", nullable: false),
                    onboarding_complete = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worker_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_worker_profiles_skills_primary_skill_id",
                        column: x => x.primary_skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_worker_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "worker_skills",
                columns: table => new
                {
                    worker_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    years_experience = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    verification_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worker_skills", x => new { x.worker_profile_id, x.skill_id });
                    table.ForeignKey(
                        name: "FK_worker_skills_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_worker_skills_worker_profiles_worker_profile_id",
                        column: x => x.worker_profile_id,
                        principalTable: "worker_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_companies_verification_state",
                table: "companies",
                column: "verification_state");

            migrationBuilder.CreateIndex(
                name: "ux_companies_trade_license_number",
                table: "companies",
                column: "trade_license_number",
                unique: true,
                filter: "trade_license_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_company_members_user_id",
                table: "company_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_skills_category_id",
                table: "skills",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_worker_profiles_primary_skill_id",
                table: "worker_profiles",
                column: "primary_skill_id");

            migrationBuilder.CreateIndex(
                name: "ux_worker_profiles_user_id",
                table: "worker_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_worker_skills_skill_id",
                table: "worker_skills",
                column: "skill_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "company_members");
            migrationBuilder.DropTable(name: "worker_skills");
            migrationBuilder.DropTable(name: "companies");
            migrationBuilder.DropTable(name: "worker_profiles");
            migrationBuilder.DropTable(name: "skills");
            migrationBuilder.DropTable(name: "skill_categories");
        }
    }
}
