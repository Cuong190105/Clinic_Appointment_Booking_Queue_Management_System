using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Sqlserver.Migrations
{
    /// <inheritdoc />
    public partial class RenamePatientLimitToRecommendedCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PatientLimit",
                table: "WorkSchedules",
                newName: "RecommendedCapacity");

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.ShiftRequests', N'PatientLimit') IS NOT NULL
                   AND COL_LENGTH(N'dbo.ShiftRequests', N'RecommendedCapacity') IS NULL
                BEGIN
                    EXEC sp_rename N'dbo.ShiftRequests.PatientLimit', N'RecommendedCapacity', N'COLUMN';
                END;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Users_UpdatedByUserId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppointmentSnapshots_Users_UpdatedByUserId",
                table: "AppointmentSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Permissions_PermissionId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Persons_PersonId",
                table: "Users");

            migrationBuilder.AddColumn<Guid>(
                name: "IdentityUserId",
                table: "Persons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NOT NULL
                BEGIN
                    UPDATE person
                    SET person.IdentityUserId = clinicUser.Id
                    FROM dbo.Persons AS person
                    INNER JOIN dbo.Users AS clinicUser ON clinicUser.PersonId = person.Id
                    WHERE person.IdentityUserId IS NULL;
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Persons_IdentityUserId",
                table: "Persons",
                column: "IdentityUserId",
                unique: true,
                filter: "[IdentityUserId] IS NOT NULL");

            migrationBuilder.DropTable(name: "RefreshTokens");
            migrationBuilder.DropTable(name: "UserRoles");
            migrationBuilder.DropTable(name: "RolePermissions");
            migrationBuilder.DropTable(name: "Users");
            migrationBuilder.DropTable(name: "Roles");
            migrationBuilder.DropTable(name: "Permissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RecommendedCapacity",
                table: "WorkSchedules",
                newName: "PatientLimit");

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.ShiftRequests', N'RecommendedCapacity') IS NOT NULL
                   AND COL_LENGTH(N'dbo.ShiftRequests', N'PatientLimit') IS NULL
                BEGIN
                    EXEC sp_rename N'dbo.ShiftRequests.RecommendedCapacity', N'PatientLimit', N'COLUMN';
                END;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Persons_IdentityUserId",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "IdentityUserId",
                table: "Persons");
        }
    }
}
