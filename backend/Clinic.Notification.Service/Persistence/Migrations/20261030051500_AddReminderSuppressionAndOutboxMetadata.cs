using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Notification.Service.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderSuppressionAndOutboxMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SuppressedAt",
                table: "Notifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentId",
                table: "DeliveryOutboxMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlotKey",
                table: "DeliveryOutboxMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReminderSuppressions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlotKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuppressedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderSuppressions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOutboxMessages_AppointmentId_Status",
                table: "DeliveryOutboxMessages",
                columns: new[] { "AppointmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReminderSuppressions_AppointmentId_SlotKey",
                table: "ReminderSuppressions",
                columns: new[] { "AppointmentId", "SlotKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReminderSuppressions");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryOutboxMessages_AppointmentId_Status",
                table: "DeliveryOutboxMessages");

            migrationBuilder.DropColumn(
                name: "SuppressedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "DeliveryOutboxMessages");

            migrationBuilder.DropColumn(
                name: "SlotKey",
                table: "DeliveryOutboxMessages");
        }
    }
}
