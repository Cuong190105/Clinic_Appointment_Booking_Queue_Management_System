using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.MedicalRecords.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientIdentityLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PersonId",
                table: "Patients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PersonId",
                table: "Patients",
                column: "PersonId",
                unique: true,
                filter: "[PersonId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_PersonId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PersonId",
                table: "Patients");
        }
    }
}
