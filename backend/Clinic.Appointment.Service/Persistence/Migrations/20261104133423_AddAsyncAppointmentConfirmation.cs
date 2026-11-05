using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Appointment.Service.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAsyncAppointmentConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConfirmationAttempts",
                table: "Appointments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextConfirmationAttemptAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PatientIdentityUserId",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SchedulingReservationId",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmationAttempts",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "NextConfirmationAttemptAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PatientIdentityUserId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SchedulingReservationId",
                table: "Appointments");
        }
    }
}
