using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Queue.Service.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancelledAppointmentTombstones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CancelledAppointments",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancelledAppointments", x => x.AppointmentId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CancelledAppointments");
        }
    }
}
