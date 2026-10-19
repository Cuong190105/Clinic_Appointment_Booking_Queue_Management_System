using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Sqlserver.Migrations;

public partial class AddBookingSlots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BookingSlots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TimeSlot = table.Column<TimeOnly>(type: "time", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BookingSlots", x => x.Id);
                table.ForeignKey(
                    name: "FK_BookingSlots_WorkSchedules_WorkScheduleId",
                    column: x => x.WorkScheduleId,
                    principalTable: "WorkSchedules",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "BookingSlotId",
            table: "Appointments",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_BookingSlots_WorkScheduleId_TimeSlot",
            table: "BookingSlots",
            columns: new[] { "WorkScheduleId", "TimeSlot" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Appointments_BookingSlotId",
            table: "Appointments",
            column: "BookingSlotId",
            unique: true,
            filter: "[BookingSlotId] IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_Appointments_BookingSlots_BookingSlotId",
            table: "Appointments",
            column: "BookingSlotId",
            principalTable: "BookingSlots",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Appointments_BookingSlots_BookingSlotId",
            table: "Appointments");

        migrationBuilder.DropIndex(
            name: "IX_Appointments_BookingSlotId",
            table: "Appointments");

        migrationBuilder.DropColumn(
            name: "BookingSlotId",
            table: "Appointments");

        migrationBuilder.DropTable(name: "BookingSlots");
    }
}
