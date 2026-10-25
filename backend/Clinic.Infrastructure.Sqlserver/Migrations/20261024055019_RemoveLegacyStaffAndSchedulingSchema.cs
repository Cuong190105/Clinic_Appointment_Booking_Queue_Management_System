using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Clinic.Infrastructure.Sqlserver.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyStaffAndSchedulingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Appointments_WorkSchedules_WorkScheduleId')
BEGIN
    ALTER TABLE [Appointments] DROP CONSTRAINT [FK_Appointments_WorkSchedules_WorkScheduleId];
END

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AppointmentSnapshots_WorkSchedules_OldWorkScheduleId')
BEGIN
    ALTER TABLE [AppointmentSnapshots] DROP CONSTRAINT [FK_AppointmentSnapshots_WorkSchedules_OldWorkScheduleId];
END");

            migrationBuilder.DropTable(
                name: "ShiftRequests");

            migrationBuilder.DropTable(
                name: "WorkHistories");

            migrationBuilder.DropTable(
                name: "WorkSchedules");

            migrationBuilder.DropTable(
                name: "Specialties");

            migrationBuilder.DropTable(
                name: "Doctors");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppointmentSnapshots_OldWorkScheduleId' AND object_id = OBJECT_ID('AppointmentSnapshots'))
BEGIN
    DROP INDEX [IX_AppointmentSnapshots_OldWorkScheduleId] ON [AppointmentSnapshots];
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Appointments_WorkScheduleId' AND object_id = OBJECT_ID('Appointments'))
BEGIN
    DROP INDEX [IX_Appointments_WorkScheduleId] ON [Appointments];
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Employees_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Specialties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstablishedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specialties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Doctors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Biography = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExperienceYears = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LicenseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Qualification = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doctors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Doctors_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecommendedCapacity = table.Column<int>(type: "int", nullable: false),
                    ShiftEnd = table.Column<TimeOnly>(type: "time", nullable: false),
                    ShiftStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftRequests_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkHistories_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkHistories_Specialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalTable: "Specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RecommendedCapacity = table.Column<int>(type: "int", nullable: false),
                    ShiftEnd = table.Column<TimeOnly>(type: "time", nullable: false),
                    ShiftStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSchedules_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Employees",
                columns: new[] { "Id", "CreatedAt", "HireDate", "IsDeleted", "ManagerId", "PersonId", "Status", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a0000003-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 2, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000001"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 3, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000002"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 4, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000003"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 5, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000004"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 6, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000005"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 7, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000006"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 8, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000007"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 9, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000008"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 10, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000009"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000010"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 11, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000010"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000011"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 12, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000011"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000012"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 1, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000012"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000013"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 2, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000013"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000014"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 3, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000014"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000015"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 4, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000015"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000016"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 5, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000016"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000017"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 6, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000017"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000018"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 7, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000018"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000019"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 8, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000019"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000020"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 9, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000020"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000021"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 10, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000021"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000022"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 11, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000022"), "Active", null }
                });

            migrationBuilder.InsertData(
                table: "Specialties",
                columns: new[] { "Id", "CreatedAt", "Description", "EstablishedDate", "IsDeleted", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a0000006-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Heart and cardiovascular care", new DateOnly(2010, 1, 15), false, "Cardiology", null },
                    { new Guid("a0000006-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Skin, hair, and nail care", new DateOnly(2012, 3, 1), false, "Dermatology", null },
                    { new Guid("a0000006-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Medical care for infants and children", new DateOnly(2008, 6, 20), false, "Pediatrics", null },
                    { new Guid("a0000006-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Bones, joints, and musculoskeletal care", new DateOnly(2011, 9, 10), false, "Orthopedics", null },
                    { new Guid("a0000006-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Primary care and general medicine", new DateOnly(2005, 4, 1), false, "General Practice", null }
                });

            migrationBuilder.InsertData(
                table: "Doctors",
                columns: new[] { "Id", "Biography", "CreatedAt", "EmployeeId", "ExperienceYears", "IsDeleted", "LicenseNumber", "Qualification", "Status", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a0000004-0000-0000-0000-000000000001"), "Sample profile for Nguyen Van An.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000001"), 6, false, "LIC-0001", "MD, Cardiology Specialist I", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000002"), "Sample profile for Tran Thi Binh.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000002"), 7, false, "LIC-0002", "MD, Cardiology Specialist II", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000003"), "Sample profile for Le Minh Chau.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000003"), 8, false, "LIC-0003", "MD, Interventional Cardiology", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000004"), "Sample profile for Pham Quoc Dung.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000004"), 9, false, "LIC-0004", "MD, Dermatology Specialist I", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000005"), "Sample profile for Hoang Thi Em.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000005"), 10, false, "LIC-0005", "MD, Cosmetic Dermatology", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000006"), "Sample profile for Vu Van Phuc.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000006"), 11, false, "LIC-0006", "MD, Dermatology Specialist II", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000007"), "Sample profile for Dang Thi Giang.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000007"), 12, false, "LIC-0007", "MD, Pediatrics Specialist I", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000008"), "Sample profile for Bui Van Hai.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000008"), 13, false, "LIC-0008", "MD, Neonatology", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000009"), "Sample profile for Ngo Thi Hoa.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000009"), 14, false, "LIC-0009", "MD, Pediatrics Specialist II", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000010"), "Sample profile for Duong Van Khoa.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000010"), 15, false, "LIC-0010", "MD, Orthopedic Surgery", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000011"), "Sample profile for Luong Thi Lan.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000011"), 16, false, "LIC-0011", "MD, Sports Medicine", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000012"), "Sample profile for Trinh Van Minh.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000012"), 17, false, "LIC-0012", "MD, Trauma Orthopedics", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000013"), "Sample profile for Cao Thi Nga.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000013"), 18, false, "LIC-0013", "MD, Family Medicine", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000014"), "Sample profile for Phan Van Quang.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000014"), 19, false, "LIC-0014", "MD, Internal Medicine", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000015"), "Sample profile for Do Thi Quyen.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000015"), 20, false, "LIC-0015", "MD, General Practice", "Active", null }
                });

            migrationBuilder.InsertData(
                table: "WorkHistories",
                columns: new[] { "Id", "CreatedAt", "DoctorId", "EndDate", "IsDeleted", "SpecialtyId", "StartDate", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a0000007-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000001"), null, false, new Guid("a0000006-0000-0000-0000-000000000001"), new DateOnly(2022, 2, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000002"), null, false, new Guid("a0000006-0000-0000-0000-000000000001"), new DateOnly(2022, 3, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000003"), null, false, new Guid("a0000006-0000-0000-0000-000000000001"), new DateOnly(2022, 4, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000004"), null, false, new Guid("a0000006-0000-0000-0000-000000000002"), new DateOnly(2022, 5, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000005"), null, false, new Guid("a0000006-0000-0000-0000-000000000002"), new DateOnly(2022, 6, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000006"), null, false, new Guid("a0000006-0000-0000-0000-000000000002"), new DateOnly(2022, 7, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000007"), null, false, new Guid("a0000006-0000-0000-0000-000000000003"), new DateOnly(2022, 8, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000008"), null, false, new Guid("a0000006-0000-0000-0000-000000000003"), new DateOnly(2022, 9, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000009"), null, false, new Guid("a0000006-0000-0000-0000-000000000003"), new DateOnly(2022, 10, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000010"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000010"), null, false, new Guid("a0000006-0000-0000-0000-000000000004"), new DateOnly(2022, 11, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000011"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000011"), null, false, new Guid("a0000006-0000-0000-0000-000000000004"), new DateOnly(2022, 12, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000012"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000012"), null, false, new Guid("a0000006-0000-0000-0000-000000000004"), new DateOnly(2023, 1, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000013"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000013"), null, false, new Guid("a0000006-0000-0000-0000-000000000005"), new DateOnly(2023, 2, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000014"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000014"), null, false, new Guid("a0000006-0000-0000-0000-000000000005"), new DateOnly(2023, 3, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000015"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000015"), null, false, new Guid("a0000006-0000-0000-0000-000000000005"), new DateOnly(2023, 4, 15), null }
                });

            migrationBuilder.InsertData(
                table: "WorkSchedules",
                columns: new[] { "Id", "CancellationReason", "CreatedAt", "Date", "DoctorId", "IsDeleted", "RecommendedCapacity", "ShiftEnd", "ShiftStart", "Status", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a0000008-0000-0000-0000-000000000001"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 23), new Guid("a0000004-0000-0000-0000-000000000001"), false, 23, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000002"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 27), new Guid("a0000004-0000-0000-0000-000000000001"), false, 30, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000003"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 31), new Guid("a0000004-0000-0000-0000-000000000001"), false, 37, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000004"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 4), new Guid("a0000004-0000-0000-0000-000000000001"), false, 23, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000005"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 8), new Guid("a0000004-0000-0000-0000-000000000001"), false, 30, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000006"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 12), new Guid("a0000004-0000-0000-0000-000000000001"), false, 37, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000007"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 16), new Guid("a0000004-0000-0000-0000-000000000001"), false, 23, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000008"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 22), new Guid("a0000004-0000-0000-0000-000000000001"), false, 30, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000009"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 26), new Guid("a0000004-0000-0000-0000-000000000001"), false, 37, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000010"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 31), new Guid("a0000004-0000-0000-0000-000000000001"), false, 23, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000011"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 24), new Guid("a0000004-0000-0000-0000-000000000002"), false, 26, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000012"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 28), new Guid("a0000004-0000-0000-0000-000000000002"), false, 33, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000013"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 1), new Guid("a0000004-0000-0000-0000-000000000002"), false, 40, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000014"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 5), new Guid("a0000004-0000-0000-0000-000000000002"), false, 26, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000015"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 9), new Guid("a0000004-0000-0000-0000-000000000002"), false, 33, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000016"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 13), new Guid("a0000004-0000-0000-0000-000000000002"), false, 40, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000017"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 17), new Guid("a0000004-0000-0000-0000-000000000002"), false, 26, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000018"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 23), new Guid("a0000004-0000-0000-0000-000000000002"), false, 33, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000019"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 27), new Guid("a0000004-0000-0000-0000-000000000002"), false, 40, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000020"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 9, 1), new Guid("a0000004-0000-0000-0000-000000000002"), false, 26, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000021"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 25), new Guid("a0000004-0000-0000-0000-000000000003"), false, 29, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000022"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 29), new Guid("a0000004-0000-0000-0000-000000000003"), false, 36, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000023"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 2), new Guid("a0000004-0000-0000-0000-000000000003"), false, 22, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000024"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 6), new Guid("a0000004-0000-0000-0000-000000000003"), false, 29, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000025"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 10), new Guid("a0000004-0000-0000-0000-000000000003"), false, 36, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000026"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 14), new Guid("a0000004-0000-0000-0000-000000000003"), false, 22, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000027"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 18), new Guid("a0000004-0000-0000-0000-000000000003"), false, 29, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000028"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 24), new Guid("a0000004-0000-0000-0000-000000000003"), false, 36, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000029"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 28), new Guid("a0000004-0000-0000-0000-000000000003"), false, 22, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000030"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 9, 2), new Guid("a0000004-0000-0000-0000-000000000003"), false, 29, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000031"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 23), new Guid("a0000004-0000-0000-0000-000000000004"), false, 32, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000032"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 27), new Guid("a0000004-0000-0000-0000-000000000004"), false, 39, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000033"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 31), new Guid("a0000004-0000-0000-0000-000000000004"), false, 25, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000034"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 4), new Guid("a0000004-0000-0000-0000-000000000004"), false, 32, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000035"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 8), new Guid("a0000004-0000-0000-0000-000000000004"), false, 39, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000036"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 12), new Guid("a0000004-0000-0000-0000-000000000004"), false, 25, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000037"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 16), new Guid("a0000004-0000-0000-0000-000000000004"), false, 32, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000038"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 22), new Guid("a0000004-0000-0000-0000-000000000004"), false, 39, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000039"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 26), new Guid("a0000004-0000-0000-0000-000000000004"), false, 25, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000040"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 31), new Guid("a0000004-0000-0000-0000-000000000004"), false, 32, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000041"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 24), new Guid("a0000004-0000-0000-0000-000000000005"), false, 35, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000042"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 28), new Guid("a0000004-0000-0000-0000-000000000005"), false, 21, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000043"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 1), new Guid("a0000004-0000-0000-0000-000000000005"), false, 28, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000044"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 5), new Guid("a0000004-0000-0000-0000-000000000005"), false, 35, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000045"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 9), new Guid("a0000004-0000-0000-0000-000000000005"), false, 21, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000046"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 13), new Guid("a0000004-0000-0000-0000-000000000005"), false, 28, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000047"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 17), new Guid("a0000004-0000-0000-0000-000000000005"), false, 35, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000048"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 23), new Guid("a0000004-0000-0000-0000-000000000005"), false, 21, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000049"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 27), new Guid("a0000004-0000-0000-0000-000000000005"), false, 28, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000050"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 9, 1), new Guid("a0000004-0000-0000-0000-000000000005"), false, 35, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000051"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 25), new Guid("a0000004-0000-0000-0000-000000000006"), false, 38, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000052"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 29), new Guid("a0000004-0000-0000-0000-000000000006"), false, 24, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000053"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 2), new Guid("a0000004-0000-0000-0000-000000000006"), false, 31, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000054"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 6), new Guid("a0000004-0000-0000-0000-000000000006"), false, 38, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000055"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 10), new Guid("a0000004-0000-0000-0000-000000000006"), false, 24, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000056"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 14), new Guid("a0000004-0000-0000-0000-000000000006"), false, 31, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000057"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 18), new Guid("a0000004-0000-0000-0000-000000000006"), false, 38, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000058"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 24), new Guid("a0000004-0000-0000-0000-000000000006"), false, 24, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000059"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 28), new Guid("a0000004-0000-0000-0000-000000000006"), false, 31, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000060"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 9, 2), new Guid("a0000004-0000-0000-0000-000000000006"), false, 38, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000061"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 23), new Guid("a0000004-0000-0000-0000-000000000007"), false, 20, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000062"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 27), new Guid("a0000004-0000-0000-0000-000000000007"), false, 27, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000063"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 31), new Guid("a0000004-0000-0000-0000-000000000007"), false, 34, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000064"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 4), new Guid("a0000004-0000-0000-0000-000000000007"), false, 20, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000065"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 8), new Guid("a0000004-0000-0000-0000-000000000007"), false, 27, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000066"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 12), new Guid("a0000004-0000-0000-0000-000000000007"), false, 34, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000067"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 16), new Guid("a0000004-0000-0000-0000-000000000007"), false, 20, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000068"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 22), new Guid("a0000004-0000-0000-0000-000000000007"), false, 27, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000069"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 26), new Guid("a0000004-0000-0000-0000-000000000007"), false, 34, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000070"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 31), new Guid("a0000004-0000-0000-0000-000000000007"), false, 20, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000071"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 24), new Guid("a0000004-0000-0000-0000-000000000008"), false, 23, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000072"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 28), new Guid("a0000004-0000-0000-0000-000000000008"), false, 30, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000073"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 1), new Guid("a0000004-0000-0000-0000-000000000008"), false, 37, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000074"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 5), new Guid("a0000004-0000-0000-0000-000000000008"), false, 23, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000075"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 9), new Guid("a0000004-0000-0000-0000-000000000008"), false, 30, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000076"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 13), new Guid("a0000004-0000-0000-0000-000000000008"), false, 37, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000077"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 17), new Guid("a0000004-0000-0000-0000-000000000008"), false, 23, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000078"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 23), new Guid("a0000004-0000-0000-0000-000000000008"), false, 30, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000079"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 27), new Guid("a0000004-0000-0000-0000-000000000008"), false, 37, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000080"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 9, 1), new Guid("a0000004-0000-0000-0000-000000000008"), false, 23, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000081"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 25), new Guid("a0000004-0000-0000-0000-000000000009"), false, 26, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000082"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 29), new Guid("a0000004-0000-0000-0000-000000000009"), false, 33, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000083"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 2), new Guid("a0000004-0000-0000-0000-000000000009"), false, 40, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000084"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 6), new Guid("a0000004-0000-0000-0000-000000000009"), false, 26, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000085"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 10), new Guid("a0000004-0000-0000-0000-000000000009"), false, 33, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000086"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 14), new Guid("a0000004-0000-0000-0000-000000000009"), false, 40, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000087"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 18), new Guid("a0000004-0000-0000-0000-000000000009"), false, 26, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000088"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 24), new Guid("a0000004-0000-0000-0000-000000000009"), false, 33, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000089"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 28), new Guid("a0000004-0000-0000-0000-000000000009"), false, 40, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000090"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 9, 2), new Guid("a0000004-0000-0000-0000-000000000009"), false, 26, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000091"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 23), new Guid("a0000004-0000-0000-0000-000000000010"), false, 29, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000092"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 27), new Guid("a0000004-0000-0000-0000-000000000010"), false, 36, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000093"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 31), new Guid("a0000004-0000-0000-0000-000000000010"), false, 22, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000094"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 4), new Guid("a0000004-0000-0000-0000-000000000010"), false, 29, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000095"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 8), new Guid("a0000004-0000-0000-0000-000000000010"), false, 36, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000096"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 12), new Guid("a0000004-0000-0000-0000-000000000010"), false, 22, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000097"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 16), new Guid("a0000004-0000-0000-0000-000000000010"), false, 29, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000098"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 22), new Guid("a0000004-0000-0000-0000-000000000010"), false, 36, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000099"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 26), new Guid("a0000004-0000-0000-0000-000000000010"), false, 22, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000100"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 31), new Guid("a0000004-0000-0000-0000-000000000010"), false, 29, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000101"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 24), new Guid("a0000004-0000-0000-0000-000000000011"), false, 32, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000102"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 28), new Guid("a0000004-0000-0000-0000-000000000011"), false, 39, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000103"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 1), new Guid("a0000004-0000-0000-0000-000000000011"), false, 25, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000104"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 5), new Guid("a0000004-0000-0000-0000-000000000011"), false, 32, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000105"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 9), new Guid("a0000004-0000-0000-0000-000000000011"), false, 39, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000106"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 13), new Guid("a0000004-0000-0000-0000-000000000011"), false, 25, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000107"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 17), new Guid("a0000004-0000-0000-0000-000000000011"), false, 32, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000108"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 23), new Guid("a0000004-0000-0000-0000-000000000011"), false, 39, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000109"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 27), new Guid("a0000004-0000-0000-0000-000000000011"), false, 25, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000110"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 9, 1), new Guid("a0000004-0000-0000-0000-000000000011"), false, 32, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000111"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 25), new Guid("a0000004-0000-0000-0000-000000000012"), false, 35, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000112"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 29), new Guid("a0000004-0000-0000-0000-000000000012"), false, 21, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000113"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 2), new Guid("a0000004-0000-0000-0000-000000000012"), false, 28, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000114"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 6), new Guid("a0000004-0000-0000-0000-000000000012"), false, 35, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000115"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 10), new Guid("a0000004-0000-0000-0000-000000000012"), false, 21, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000116"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 14), new Guid("a0000004-0000-0000-0000-000000000012"), false, 28, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000117"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 18), new Guid("a0000004-0000-0000-0000-000000000012"), false, 35, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000118"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 24), new Guid("a0000004-0000-0000-0000-000000000012"), false, 21, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000119"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 28), new Guid("a0000004-0000-0000-0000-000000000012"), false, 28, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000120"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 9, 2), new Guid("a0000004-0000-0000-0000-000000000012"), false, 35, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000121"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 23), new Guid("a0000004-0000-0000-0000-000000000013"), false, 38, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000122"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 27), new Guid("a0000004-0000-0000-0000-000000000013"), false, 24, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000123"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 31), new Guid("a0000004-0000-0000-0000-000000000013"), false, 31, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000124"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 4), new Guid("a0000004-0000-0000-0000-000000000013"), false, 38, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000125"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 8), new Guid("a0000004-0000-0000-0000-000000000013"), false, 24, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000126"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 12), new Guid("a0000004-0000-0000-0000-000000000013"), false, 31, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000127"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 16), new Guid("a0000004-0000-0000-0000-000000000013"), false, 38, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000128"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 22), new Guid("a0000004-0000-0000-0000-000000000013"), false, 24, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000129"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 26), new Guid("a0000004-0000-0000-0000-000000000013"), false, 31, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000130"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 31), new Guid("a0000004-0000-0000-0000-000000000013"), false, 38, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000131"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 24), new Guid("a0000004-0000-0000-0000-000000000014"), false, 20, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000132"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 28), new Guid("a0000004-0000-0000-0000-000000000014"), false, 27, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000133"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 1), new Guid("a0000004-0000-0000-0000-000000000014"), false, 34, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000134"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 5), new Guid("a0000004-0000-0000-0000-000000000014"), false, 20, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000135"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 9), new Guid("a0000004-0000-0000-0000-000000000014"), false, 27, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000136"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 13), new Guid("a0000004-0000-0000-0000-000000000014"), false, 34, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000137"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 17), new Guid("a0000004-0000-0000-0000-000000000014"), false, 20, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000138"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 23), new Guid("a0000004-0000-0000-0000-000000000014"), false, 27, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000139"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 27), new Guid("a0000004-0000-0000-0000-000000000014"), false, 34, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000140"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 9, 1), new Guid("a0000004-0000-0000-0000-000000000014"), false, 20, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000141"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 25), new Guid("a0000004-0000-0000-0000-000000000015"), false, 23, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000142"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 7, 29), new Guid("a0000004-0000-0000-0000-000000000015"), false, 30, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000143"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 2), new Guid("a0000004-0000-0000-0000-000000000015"), false, 37, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000144"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 6), new Guid("a0000004-0000-0000-0000-000000000015"), false, 23, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000145"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 10), new Guid("a0000004-0000-0000-0000-000000000015"), false, 30, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000146"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 14), new Guid("a0000004-0000-0000-0000-000000000015"), false, 37, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000147"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 18), new Guid("a0000004-0000-0000-0000-000000000015"), false, 23, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000148"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 24), new Guid("a0000004-0000-0000-0000-000000000015"), false, 30, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000149"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 8, 28), new Guid("a0000004-0000-0000-0000-000000000015"), false, 37, new TimeOnly(17, 30, 0), new TimeOnly(13, 30, 0), "Active", null },
                    { new Guid("a0000008-0000-0000-0000-000000000150"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2026, 9, 2), new Guid("a0000004-0000-0000-0000-000000000015"), false, 23, new TimeOnly(11, 30, 0), new TimeOnly(7, 30, 0), "Active", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSnapshots_OldWorkScheduleId",
                table: "AppointmentSnapshots",
                column: "OldWorkScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_WorkScheduleId",
                table: "Appointments",
                column: "WorkScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_EmployeeId",
                table: "Doctors",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PersonId",
                table: "Employees",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRequests_DoctorId",
                table: "ShiftRequests",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Specialties_Name",
                table: "Specialties",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkHistories_DoctorId",
                table: "WorkHistories",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkHistories_SpecialtyId",
                table: "WorkHistories",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_DoctorId",
                table: "WorkSchedules",
                column: "DoctorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_WorkSchedules_WorkScheduleId",
                table: "Appointments",
                column: "WorkScheduleId",
                principalTable: "WorkSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentSnapshots_WorkSchedules_OldWorkScheduleId",
                table: "AppointmentSnapshots",
                column: "OldWorkScheduleId",
                principalTable: "WorkSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
