using Clinic.Identity.Service.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Clinic.Identity.Service.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("202610200002_AddPasswordResetTokens")]
public sealed class AddPasswordResetTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PasswordResetTokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TokenHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                table.ForeignKey("FK_PasswordResetTokens_Users_UserId", x => x.UserId,
                    "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_PasswordResetTokens_TokenHash", "PasswordResetTokens", "TokenHash", unique: true);
        migrationBuilder.CreateIndex("IX_PasswordResetTokens_UserId_ExpiresAt", "PasswordResetTokens", new[] { "UserId", "ExpiresAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("PasswordResetTokens");
}
