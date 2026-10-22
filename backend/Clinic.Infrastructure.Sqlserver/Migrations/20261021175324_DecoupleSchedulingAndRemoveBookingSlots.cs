using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Sqlserver.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleSchedulingAndRemoveBookingSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DoctorId",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "DoctorName",
                table: "Appointments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduleDate",
                table: "Appointments",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "SpecialtyName",
                table: "Appointments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE a
SET a.DoctorId = ws.DoctorId,
    a.DoctorName = p.FullName,
    a.SpecialtyName = s.Name,
    a.ScheduleDate = ws.Date
FROM [Appointments] a
INNER JOIN [WorkSchedules] ws ON a.WorkScheduleId = ws.Id
INNER JOIN [Doctors] d ON ws.DoctorId = d.Id
INNER JOIN [Employees] e ON d.EmployeeId = e.Id
INNER JOIN [Persons] p ON e.PersonId = p.Id
LEFT JOIN [WorkHistories] wh ON d.Id = wh.DoctorId AND wh.EndDate IS NULL AND wh.IsDeleted = 0
LEFT JOIN [Specialties] s ON wh.SpecialtyId = s.Id;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Appointments_WorkSchedules_WorkScheduleId')
BEGIN
    ALTER TABLE [Appointments] DROP CONSTRAINT [FK_Appointments_WorkSchedules_WorkScheduleId];
END

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AppointmentSnapshots_WorkSchedules_OldWorkScheduleId')
BEGIN
    ALTER TABLE [AppointmentSnapshots] DROP CONSTRAINT [FK_AppointmentSnapshots_WorkSchedules_OldWorkScheduleId];
END

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Appointments_BookingSlots_BookingSlotId')
BEGIN
    ALTER TABLE [Appointments] DROP CONSTRAINT [FK_Appointments_BookingSlots_BookingSlotId];
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Appointments_BookingSlotId' AND object_id = OBJECT_ID('Appointments'))
BEGIN
    DROP INDEX [IX_Appointments_BookingSlotId] ON [Appointments];
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'BookingSlotId')
BEGIN
    ALTER TABLE [Appointments] DROP COLUMN [BookingSlotId];
END

IF OBJECT_ID('BookingSlots', 'U') IS NOT NULL
BEGIN
    DROP TABLE [BookingSlots];
END
");

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000001"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 23), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000002"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 23), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000003"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 23), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000004"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 23), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000005"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 23), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000006"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 23), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000007"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 23), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000008"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 23), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000009"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 23), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000010"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 23), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000011"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 27), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000012"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 27), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000013"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 27), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000014"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 27), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000015"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 27), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000016"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 31), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000017"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 31), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000018"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 31), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000019"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 31), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000020"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 31), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000021"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 31), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000022"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 31), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000023"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 31), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000024"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 31), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000025"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 7, 31), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000026"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 4), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000027"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 4), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000028"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 4), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000029"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 4), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000030"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 4), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000031"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 12), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000032"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 12), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000033"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 12), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000034"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 12), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000035"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 12), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000036"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 12), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000037"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 16), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000038"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 16), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000039"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 16), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000040"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 16), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000041"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 16), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000042"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 16), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000043"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 16), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000044"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 22), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000045"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 22), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000046"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 22), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000047"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 22), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000048"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 22), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000049"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 22), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000050"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 22), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000051"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 22), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000052"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 22), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000053"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 22), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000054"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 31), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000055"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000001"), "Nguyen Van An", new DateOnly(2026, 8, 31), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000056"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 7, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000057"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 7, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000058"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 7, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000059"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 7, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000060"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 7, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000061"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 7, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000062"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 7, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000063"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 7, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000064"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 7, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000065"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 7, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000066"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 1), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000067"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 1), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000068"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 1), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000069"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 5), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000070"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 5), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000071"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 5), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000072"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 9), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000073"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 9), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000074"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 9), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000075"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 9), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000076"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 13), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000077"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 13), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000078"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 13), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000079"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 13), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000080"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 13), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000081"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 13), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000082"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 13), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000083"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 13), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000084"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 13), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000085"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 13), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000086"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 17), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000087"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 17), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000088"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 17), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000089"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 17), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000090"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 17), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000091"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 17), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000092"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000093"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000094"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000095"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000096"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000097"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000098"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000099"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 27), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000100"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 27), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000101"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 27), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000102"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 8, 27), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000103"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 9, 1), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000104"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 9, 1), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000105"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 9, 1), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000106"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 9, 1), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000107"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 9, 1), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000108"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 9, 1), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000109"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 9, 1), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000110"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000002"), "Tran Thi Binh", new DateOnly(2026, 9, 1), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000111"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 7, 25), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000112"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 7, 25), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000113"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 7, 25), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000114"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 7, 25), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000115"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 7, 29), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000116"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 7, 29), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000117"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 7, 29), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000118"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 7, 29), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000119"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 2), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000120"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 2), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000121"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 2), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000122"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 2), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000123"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 2), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000124"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 2), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000125"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 2), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000126"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 2), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000127"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 2), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000128"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 2), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000129"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 6), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000130"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 6), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000131"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 6), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000132"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 6), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000133"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 6), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000134"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 6), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000135"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 6), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000136"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 6), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000137"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 10), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000138"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 10), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000139"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 14), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000140"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 14), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000141"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 14), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000142"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 14), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000143"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 14), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000144"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 14), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000145"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 14), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000146"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 18), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000147"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 18), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000148"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 18), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000149"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 18), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000150"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 18), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000151"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 18), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000152"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 18), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000153"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000154"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000155"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000156"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000157"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000158"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000159"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000160"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000161"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000003"), "Le Minh Chau", new DateOnly(2026, 8, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000162"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 23), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000163"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 23), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000164"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 23), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000165"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 23), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000166"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 23), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000167"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 23), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000168"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 23), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000169"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 23), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000170"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 27), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000171"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 27), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000172"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 27), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000173"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 27), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000174"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 27), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000175"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 27), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000176"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 27), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000177"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 31), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000178"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 7, 31), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000179"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 4), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000180"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 4), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000181"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 4), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000182"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 4), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000183"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 8), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000184"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 8), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000185"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 8), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000186"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 8), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000187"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 8), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000188"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 12), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000189"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 12), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000190"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 12), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000191"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 12), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000192"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 12), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000193"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 12), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000194"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 12), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000195"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 12), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000196"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 12), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000197"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 16), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000198"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 16), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000199"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 16), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000200"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 16), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000201"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 16), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000202"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 16), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000203"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 16), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000204"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 16), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000205"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 16), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000206"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 22), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000207"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 22), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000208"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 26), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000209"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 26), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000210"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 26), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000211"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 26), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000212"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 26), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000213"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 26), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000214"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 26), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000215"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 26), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000216"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 26), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000217"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 26), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000218"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 31), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000219"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 31), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000220"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000004"), "Pham Quoc Dung", new DateOnly(2026, 8, 31), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000221"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 7, 24), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000222"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 7, 24), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000223"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 7, 24), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000224"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 7, 24), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000225"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 7, 24), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000226"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 7, 28), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000227"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 7, 28), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000228"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 7, 28), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000229"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 7, 28), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000230"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 7, 28), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000231"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 7, 28), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000232"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 7, 28), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000233"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 8, 1), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000234"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 8, 9), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000235"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 8, 9), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000236"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 8, 9), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000237"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 8, 9), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000238"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 8, 9), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000239"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 8, 13), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000240"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 8, 17), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000241"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 8, 23), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000242"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 8, 27), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000243"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 9, 1), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000244"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 9, 1), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000245"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 9, 1), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000246"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 9, 1), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000247"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 9, 1), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000248"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000005"), "Hoang Thi Em", new DateOnly(2026, 9, 1), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000249"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 7, 25), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000250"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 7, 25), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000251"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 7, 29), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000252"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 7, 29), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000253"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 7, 29), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000254"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 7, 29), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000255"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 7, 29), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000256"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 7, 29), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000257"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 7, 29), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000258"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 7, 29), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000259"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 7, 29), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000260"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 7, 29), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000261"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 2), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000262"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 2), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000263"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 2), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000264"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 2), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000265"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 2), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000266"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 2), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000267"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 6), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000268"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 6), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000269"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 6), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000270"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 6), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000271"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 6), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000272"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 6), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000273"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 10), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000274"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 14), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000275"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 14), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000276"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 14), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000277"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 14), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000278"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 14), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000279"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 14), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000280"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 14), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000281"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 14), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000282"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 14), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000283"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 24), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000284"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 24), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000285"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 24), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000286"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 24), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000287"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 24), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000288"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 24), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000289"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 24), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000290"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 24), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000291"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 8, 28), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000292"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 9, 2), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000293"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 9, 2), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000294"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 9, 2), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000295"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 9, 2), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000296"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000006"), "Vu Van Phuc", new DateOnly(2026, 9, 2), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000297"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000298"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000299"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000300"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000301"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000302"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000303"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 23), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000304"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 27), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000305"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 27), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000306"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 27), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000307"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 27), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000308"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 27), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000309"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 31), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000310"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 31), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000311"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 31), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000312"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 31), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000313"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 31), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000314"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 31), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000315"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 7, 31), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000316"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 4), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000317"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 12), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000318"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 12), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000319"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 12), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000320"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 12), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000321"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 12), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000322"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 12), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000323"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 16), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000324"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 16), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000325"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 16), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000326"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 22), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000327"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 22), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000328"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 22), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000329"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 22), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000330"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 22), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000331"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 26), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000332"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 26), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000333"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 26), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000334"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 26), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000335"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 26), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000336"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000007"), "Dang Thi Giang", new DateOnly(2026, 8, 26), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000337"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000338"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000339"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000340"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000341"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000342"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000343"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000344"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000345"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000346"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 24), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000347"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000348"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000349"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000350"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000351"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000352"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000353"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000354"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 7, 28), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000355"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 1), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000356"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 1), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000357"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 1), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000358"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 5), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000359"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 5), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000360"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 5), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000361"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 5), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000362"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 5), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000363"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 5), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000364"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 5), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000365"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 5), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000366"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 5), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000367"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 9), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000368"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 9), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000369"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 9), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000370"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 9), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000371"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 9), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000372"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 9), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000373"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 9), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000374"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 9), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000375"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 13), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000376"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 13), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000377"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 13), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000378"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 13), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000379"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 17), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000380"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 17), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000381"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 17), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000382"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 17), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000383"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 17), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000384"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 17), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000385"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000386"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000387"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000388"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000389"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000390"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000391"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000392"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 8, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000393"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 9, 1), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000394"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 9, 1), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000395"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 9, 1), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000396"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 9, 1), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000397"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 9, 1), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000398"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 9, 1), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000399"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 9, 1), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000400"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 9, 1), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000401"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000008"), "Bui Van Hai", new DateOnly(2026, 9, 1), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000402"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 7, 25), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000403"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 7, 25), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000404"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 7, 29), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000405"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 7, 29), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000406"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 7, 29), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000407"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 7, 29), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000408"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 7, 29), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000409"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 7, 29), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000410"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 7, 29), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000411"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 7, 29), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000412"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 7, 29), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000413"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 2), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000414"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 2), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000415"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 2), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000416"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 2), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000417"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 6), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000418"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 6), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000419"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 6), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000420"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 6), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000421"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 6), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000422"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 6), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000423"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 6), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000424"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 6), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000425"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 6), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000426"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 6), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000427"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 10), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000428"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 10), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000429"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 10), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000430"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 10), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000431"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 10), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000432"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 14), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000433"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 14), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000434"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 14), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000435"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 14), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000436"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 14), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000437"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 14), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000438"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 14), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000439"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 14), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000440"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 14), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000441"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 14), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000442"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 18), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000443"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 18), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000444"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 18), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000445"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 18), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000446"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 18), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000447"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 18), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000448"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 18), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000449"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 18), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000450"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 18), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000451"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 24), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000452"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 24), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000453"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 24), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000454"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 24), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000455"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 28), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000456"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 28), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000457"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 28), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000458"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 8, 28), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000459"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 9, 2), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000460"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 9, 2), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000461"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 9, 2), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000462"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 9, 2), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000463"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000009"), "Ngo Thi Hoa", new DateOnly(2026, 9, 2), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000464"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 7, 23), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000465"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 7, 27), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000466"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 7, 31), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000467"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 7, 31), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000468"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 7, 31), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000469"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 7, 31), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000470"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 7, 31), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000471"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 4), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000472"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 4), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000473"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 4), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000474"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 4), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000475"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 4), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000476"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 4), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000477"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 4), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000478"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 4), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000479"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 8), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000480"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 8), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000481"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 8), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000482"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 8), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000483"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 8), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000484"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 8), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000485"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 8), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000486"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 8), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000487"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 8), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000488"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 8), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000489"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 16), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000490"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 16), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000491"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 16), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000492"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 22), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000493"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 22), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000494"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 22), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000495"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 22), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000496"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 26), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000497"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 26), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000498"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 26), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000499"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 26), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000500"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 31), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000501"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000010"), "Duong Van Khoa", new DateOnly(2026, 8, 31), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000502"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 7, 24), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000503"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 7, 24), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000504"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 7, 24), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000505"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 7, 28), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000506"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 1), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000507"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 1), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000508"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 1), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000509"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 1), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000510"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 1), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000511"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 1), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000512"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 1), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000513"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 1), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000514"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 5), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000515"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 5), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000516"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 5), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000517"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 9), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000518"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 9), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000519"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 9), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000520"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 9), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000521"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 13), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000522"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 13), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000523"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 13), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000524"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 13), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000525"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 13), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000526"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 13), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000527"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 13), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000528"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 17), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000529"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 17), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000530"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 17), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000531"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 17), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000532"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 23), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000533"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 8, 27), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000534"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 9, 1), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000535"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 9, 1), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000536"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000011"), "Luong Thi Lan", new DateOnly(2026, 9, 1), "Cardiology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000537"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 7, 25), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000538"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 7, 25), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000539"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 7, 25), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000540"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 7, 25), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000541"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 7, 25), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000542"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 7, 25), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000543"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 7, 25), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000544"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 7, 29), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000545"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 7, 29), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000546"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 2), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000547"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 2), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000548"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 2), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000549"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 2), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000550"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 2), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000551"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 2), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000552"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 2), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000553"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 2), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000554"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 6), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000555"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 6), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000556"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 10), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000557"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 10), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000558"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 10), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000559"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 10), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000560"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 10), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000561"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 10), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000562"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 10), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000563"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 10), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000564"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 14), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000565"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 14), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000566"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 14), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000567"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 14), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000568"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 14), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000569"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 18), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000570"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 18), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000571"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 18), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000572"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 18), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000573"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 18), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000574"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 18), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000575"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 18), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000576"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 18), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000577"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 24), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000578"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 24), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000579"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000580"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000581"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000582"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000583"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000584"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000585"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000586"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000587"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 8, 28), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000588"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 9, 2), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000589"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 9, 2), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000590"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000012"), "Trinh Van Minh", new DateOnly(2026, 9, 2), "Dermatology" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000591"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 7, 23), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000592"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 7, 23), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000593"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 7, 23), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000594"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 7, 23), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000595"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 7, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000596"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 7, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000597"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 7, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000598"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 7, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000599"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 7, 27), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000600"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 7, 31), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000601"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 7, 31), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000602"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 4), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000603"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 4), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000604"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 4), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000605"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 4), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000606"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 4), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000607"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 4), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000608"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 4), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000609"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 4), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000610"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 8), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000611"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 8), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000612"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 8), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000613"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 8), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000614"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 8), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000615"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 8), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000616"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 8), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000617"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 8), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000618"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 8), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000619"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 12), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000620"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 12), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000621"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 12), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000622"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 12), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000623"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 16), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000624"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 16), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000625"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 16), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000626"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 16), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000627"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 16), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000628"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 16), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000629"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 16), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000630"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 16), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000631"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 16), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000632"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 22), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000633"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 22), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000634"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 22), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000635"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 26), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000636"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 26), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000637"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 26), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000638"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 26), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000639"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 26), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000640"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 26), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000641"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 31), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000642"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000013"), "Cao Thi Nga", new DateOnly(2026, 8, 31), "Pediatrics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000643"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 7, 24), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000644"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 7, 24), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000645"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 7, 24), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000646"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 7, 28), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000647"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 7, 28), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000648"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 7, 28), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000649"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 1), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000650"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 5), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000651"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 5), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000652"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 5), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000653"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 5), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000654"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 5), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000655"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 5), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000656"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 5), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000657"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 5), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000658"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 5), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000659"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 9), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000660"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 9), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000661"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 9), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000662"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 13), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000663"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 13), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000664"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 13), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000665"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 13), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000666"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 13), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000667"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 13), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000668"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 13), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000669"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 17), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000670"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 17), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000671"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 17), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000672"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 23), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000673"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 23), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000674"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 23), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000675"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 8, 23), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000676"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 9, 1), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000677"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 9, 1), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000678"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 9, 1), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000679"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 9, 1), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000680"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 9, 1), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000681"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 9, 1), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000682"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 9, 1), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000683"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 9, 1), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000684"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000014"), "Phan Van Quang", new DateOnly(2026, 9, 1), "Orthopedics" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000685"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 7, 25), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000686"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 7, 25), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000687"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 7, 25), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000688"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 7, 25), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000689"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 7, 25), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000690"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 7, 29), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000691"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 7, 29), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000692"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 7, 29), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000693"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 7, 29), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000694"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 7, 29), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000695"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 7, 29), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000696"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 7, 29), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000697"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 2), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000698"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 2), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000699"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 2), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000700"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 2), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000701"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 2), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000702"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 2), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000703"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 6), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000704"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 6), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000705"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 6), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000706"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 6), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000707"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 10), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000708"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 10), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000709"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 10), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000710"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 10), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000711"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 10), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000712"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 10), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000713"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 10), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000714"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 10), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000715"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 14), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000716"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 14), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000717"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 14), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000718"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 14), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000719"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 14), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000720"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 14), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000721"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 14), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000722"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 18), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000723"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 18), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000724"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 18), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000725"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 18), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000726"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 18), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000727"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 18), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000728"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 18), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000729"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 18), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000730"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 18), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000731"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 24), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000732"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 24), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000733"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 24), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000734"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 8, 28), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000735"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 9, 2), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000736"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 9, 2), "General Practice" });

            migrationBuilder.UpdateData(
                table: "Appointments",
                keyColumn: "Id",
                keyValue: new Guid("a0000009-0000-0000-0000-000000000737"),
                columns: new[] { "DoctorId", "DoctorName", "ScheduleDate", "SpecialtyName" },
                values: new object[] { new Guid("a0000004-0000-0000-0000-000000000015"), "Do Thi Quyen", new DateOnly(2026, 9, 2), "General Practice" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoctorId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DoctorName",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ScheduleDate",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SpecialtyName",
                table: "Appointments");
        }
    }
}
