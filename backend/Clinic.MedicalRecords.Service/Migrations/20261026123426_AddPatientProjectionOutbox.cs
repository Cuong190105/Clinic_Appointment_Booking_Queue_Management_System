using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.MedicalRecords.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientProjectionOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientProjectionOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientProjectionOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientProjectionOutbox_PatientId",
                table: "PatientProjectionOutbox",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientProjectionOutbox_ProcessedAt_CreatedAt",
                table: "PatientProjectionOutbox",
                columns: new[] { "ProcessedAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientProjectionOutbox");
        }
    }
}
