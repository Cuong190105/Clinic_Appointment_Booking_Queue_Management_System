using Clinic.Identity.Service.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Clinic.Identity.Service.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("202610200001_InitialIdentitySchema")]
public sealed class InitialIdentitySchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("Persons", table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
            PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
            Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
            DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
            Gender = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
            Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_Persons", x => x.Id));

        migrationBuilder.CreateTable("Roles", table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
            Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_Roles", x => x.Id));

        migrationBuilder.CreateTable("Permissions", table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
            Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_Permissions", x => x.Id));

        migrationBuilder.CreateTable("Users", table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
            EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
            DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
            Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
            PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
            IsActive = table.Column<bool>(type: "bit", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_Users", x => x.Id);
            table.ForeignKey("FK_Users_Persons_PersonId", x => x.PersonId, "Persons", "Id", onDelete: ReferentialAction.Restrict);
        });

        migrationBuilder.CreateTable("UserRoles", table => new
        {
            UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
            table.ForeignKey("FK_UserRoles_Roles_RoleId", x => x.RoleId, "Roles", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_UserRoles_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateTable("RefreshTokens", table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            TokenHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
            IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_RefreshTokens", x => x.Id);
            table.ForeignKey("FK_RefreshTokens_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateTable("RolePermissions", table => new
        {
            RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
            table.ForeignKey("FK_RolePermissions_Permissions_PermissionId", x => x.PermissionId, "Permissions", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_RolePermissions_Roles_RoleId", x => x.RoleId, "Roles", "Id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateIndex("IX_Persons_PhoneNumber", "Persons", "PhoneNumber", unique: true, filter: "[PhoneNumber] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_Roles_Name", "Roles", "Name", unique: true);
        migrationBuilder.CreateIndex("IX_Permissions_Name", "Permissions", "Name", unique: true);
        migrationBuilder.CreateIndex("IX_Users_PersonId", "Users", "PersonId", unique: true);
        migrationBuilder.CreateIndex("IX_Users_Username", "Users", "Username", unique: true);
        migrationBuilder.CreateIndex("IX_UserRoles_RoleId", "UserRoles", "RoleId");
        migrationBuilder.CreateIndex("IX_RefreshTokens_TokenHash", "RefreshTokens", "TokenHash", unique: true);
        migrationBuilder.CreateIndex("IX_RefreshTokens_UserId", "RefreshTokens", "UserId");
        migrationBuilder.CreateIndex("IX_RolePermissions_PermissionId", "RolePermissions", "PermissionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("RefreshTokens");
        migrationBuilder.DropTable("RolePermissions");
        migrationBuilder.DropTable("UserRoles");
        migrationBuilder.DropTable("Permissions");
        migrationBuilder.DropTable("Roles");
        migrationBuilder.DropTable("Users");
        migrationBuilder.DropTable("Persons");
    }
}
