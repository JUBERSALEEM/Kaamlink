using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaamLink.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Phase 06: email OTP/password identities. The column is nullable so existing
    /// phone-only users keep a null email; PostgreSQL allows multiple nulls in the
    /// unique index, while two confirmed emails still map to one KaamLink user.
    /// </summary>
    public partial class Phase06EmailAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_users_email",
                table: "users");

            migrationBuilder.DropColumn(
                name: "email",
                table: "users");
        }
    }
}
