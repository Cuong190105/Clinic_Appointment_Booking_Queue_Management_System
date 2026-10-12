using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Clinic.Infrastructure.Sqlserver.Migrations
{
    /// <inheritdoc />
    public partial class SeedDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Persons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueueCounters",
                columns: table => new
                {
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrentNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueCounters", x => new { x.DoctorId, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Specialties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstablishedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specialties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SendTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InsuranceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmergencyContact = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Patients_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Doctors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LicenseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExperienceYears = table.Column<int>(type: "int", nullable: false),
                    Qualification = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Biography = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    ShiftEnd = table.Column<TimeOnly>(type: "time", nullable: false),
                    PatientLimit = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    ShiftEnd = table.Column<TimeOnly>(type: "time", nullable: false),
                    PatientLimit = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TimeSlot = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsWalkIn = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_WorkSchedules_WorkScheduleId",
                        column: x => x.WorkScheduleId,
                        principalTable: "WorkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldWorkScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeSlot = table.Column<TimeOnly>(type: "time", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentSnapshots_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentSnapshots_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentSnapshots_WorkSchedules_OldWorkScheduleId",
                        column: x => x.OldWorkScheduleId,
                        principalTable: "WorkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QueueTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QueueNumber = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CalledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueueTickets_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicalReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QueueTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symptoms = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Diagnosis = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Prescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExamStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExamEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalReports_QueueTickets_QueueTicketId",
                        column: x => x.QueueTicketId,
                        principalTable: "QueueTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "CreatedAt", "Description", "IsDeleted", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Create appointments", false, "appointment.create", null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View appointments", false, "appointment.view", null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cancel appointments (deprecated - dùng appointment.cancel.own/.any)", true, "appointment.cancel", null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Call next in queue", false, "queue.call-next", null },
                    { new Guid("10000000-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View own doctor profile (Doctor)", false, "doctor.view.own", null },
                    { new Guid("10000000-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Edit doctor profile (deprecated - dùng doctor.edit.own/.any)", true, "doctor.edit", null },
                    { new Guid("10000000-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Manage users", false, "user.manage", null },
                    { new Guid("10000000-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Manage roles and permissions", false, "role.manage", null },
                    { new Guid("10000000-0000-0000-0000-000000000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Update appointments", false, "appointment.update", null },
                    { new Guid("10000000-0000-0000-0000-00000000000c"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View doctor's queue", false, "doctor.queue.view", null },
                    { new Guid("10000000-0000-0000-0000-00000000000d"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Create doctor profile", false, "doctor.create", null },
                    { new Guid("10000000-0000-0000-0000-00000000000e"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cancel own appointment (Patient)", false, "appointment.edit.own", null },
                    { new Guid("10000000-0000-0000-0000-00000000000f"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cancel any appointment (Admin/Receptionist)", false, "appointment.edit.any", null },
                    { new Guid("10000000-0000-0000-0000-000000000013"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Edit own doctor profile (Doctor)", false, "doctor.edit.own", null },
                    { new Guid("10000000-0000-0000-0000-000000000014"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Edit any doctor profile (Admin)", false, "doctor.edit.any", null },
                    { new Guid("10000000-0000-0000-0000-000000000015"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View own employee profile (Employee)", false, "employee.view.own", null },
                    { new Guid("10000000-0000-0000-0000-000000000016"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Create employee profile", false, "employee.create", null },
                    { new Guid("10000000-0000-0000-0000-000000000017"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Edit any employee profile (Admin)", false, "employee.edit.any", null },
                    { new Guid("10000000-0000-0000-0000-000000000018"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Edit own employee profile (Employee)", false, "employee.edit.own", null },
                    { new Guid("10000000-0000-0000-0000-000000000019"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View any employee profile (Admin)", false, "employee.view.any", null },
                    { new Guid("10000000-0000-0000-0000-000000000020"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View any doctor profile (Admin)", false, "doctor.view.any", null },
                    { new Guid("1000000a-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Confirm appointments", false, "appointment.confirm", null },
                    { new Guid("1000000a-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Reschedule appointments", false, "appointment.reschedule", null },
                    { new Guid("20000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View medical reports", true, "medical-report.view", null },
                    { new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Create medical reports", false, "medical-report.create", null },
                    { new Guid("20000000-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View patient medical history", true, "patient-history.view", null },
                    { new Guid("20000000-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Manage any doctor's shifts", false, "shift.manage", null },
                    { new Guid("20000000-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Manage own shift requests", false, "shift.self-manage", null },
                    { new Guid("20000000-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View queue", false, "queue.view", null },
                    { new Guid("20000000-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Check in a patient into queue", false, "queue.check-in", null },
                    { new Guid("20000000-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Skip a patient in queue", false, "queue.skip", null },
                    { new Guid("20000000-0000-0000-0000-000000000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Set priority in queue", false, "queue.priority", null },
                    { new Guid("20000000-0000-0000-0000-00000000000a"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View own medical reports (Patient)", false, "medical-report.view.own", null },
                    { new Guid("20000000-0000-0000-0000-00000000000b"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View medical reports of patients in own exam cases (Doctor)", false, "medical-report.view.related", null },
                    { new Guid("20000000-0000-0000-0000-00000000000c"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View any medical report (Admin)", false, "medical-report.view.any", null },
                    { new Guid("20000000-0000-0000-0000-00000000000d"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View own medical history (Patient)", false, "patient-history.view.own", null },
                    { new Guid("20000000-0000-0000-0000-00000000000e"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View medical history of patients in own exam cases (Doctor)", false, "patient-history.view.related", null },
                    { new Guid("20000000-0000-0000-0000-00000000000f"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View any patient's medical history (Admin)", false, "patient-history.view.any", null },
                    { new Guid("2000000a-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Manage notifications", false, "notification.manage", null },
                    { new Guid("2000000a-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View clinic-wide reports", false, "report.view", null },
                    { new Guid("2000000a-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Manage medical specialties", false, "specialty.manage", null },
                    { new Guid("2000000a-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View appointment slots", false, "slot.view", null },
                    { new Guid("2000000a-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Manage appointment slots", false, "slot.manage", null },
                    { new Guid("2000000a-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Approve/reject doctor shift-change suggestions (Receptionist)", false, "shift.suggestion.manage", null },
                    { new Guid("2000000a-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Create any patient record (Receptionist)", false, "patient.create.any", null },
                    { new Guid("2000000a-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Edit own patient record (Patient)", false, "patient.edit.own", null },
                    { new Guid("2000000a-0000-0000-0000-000000000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Edit any patient record (Receptionist)", false, "patient.edit.any", null },
                    { new Guid("2000000a-0000-0000-0000-000000000010"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View own patient record (Patient)", false, "patient.view.own", null },
                    { new Guid("2000000a-0000-0000-0000-000000000011"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "View any patient record (Receptionist)", false, "patient.view.any", null },
                    { new Guid("2000000b-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Manage doctor own shift suggestions (Doctor)", false, "shift.suggestion.self-manage", null },
                    { new Guid("2000000b-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Start exam for any doctor's queue ticket (Admin/Receptionist)", false, "queue.start-exam.any", null },
                    { new Guid("2000000b-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Complete exam for own queue ticket (Doctor)", false, "queue.complete-exam.own", null },
                    { new Guid("2000000b-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Complete exam for any doctor's queue ticket (Admin/Receptionist)", false, "queue.complete-exam.any", null },
                    { new Guid("2000000b-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Start exam for own queue ticket (Doctor)", false, "queue.start-exam.own", null }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsDeleted", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Administrator role", false, "Admin", null },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Patient role", false, "Patient", null },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Receptionist role", false, "Receptionist", null },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Doctor role", false, "Doctor", null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-00000000000c"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-00000000000d"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-00000000000f"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000017"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000018"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000019"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000020"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-00000000000c"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-00000000000f"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("2000000a-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("2000000a-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("2000000a-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("2000000a-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("2000000a-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("2000000b-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("2000000b-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("10000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("10000000-0000-0000-0000-00000000000e"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("1000000a-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-00000000000a"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-00000000000d"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("2000000a-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("2000000a-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("2000000a-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("10000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("10000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("10000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("10000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("10000000-0000-0000-0000-00000000000c"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("10000000-0000-0000-0000-00000000000f"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("10000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("10000000-0000-0000-0000-000000000018"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("1000000a-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("20000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("20000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("20000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("20000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("2000000a-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("2000000a-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("2000000a-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("2000000a-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("2000000a-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("2000000a-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("2000000a-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("2000000b-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("2000000b-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("10000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("10000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("10000000-0000-0000-0000-00000000000c"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("10000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("10000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("10000000-0000-0000-0000-000000000018"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("20000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("20000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("20000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("20000000-0000-0000-0000-00000000000b"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("20000000-0000-0000-0000-00000000000e"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("2000000a-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("2000000a-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("2000000b-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("2000000b-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000004") },
                    { new Guid("2000000b-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000004") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId",
                table: "Appointments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TimeSlot_WorkScheduleId",
                table: "Appointments",
                columns: new[] { "TimeSlot", "WorkScheduleId" },
                unique: true,
                filter: "[IsWalkIn] = 0 AND [Status] <> 'Cancelled' AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_UpdatedByUserId",
                table: "Appointments",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_WorkScheduleId",
                table: "Appointments",
                column: "WorkScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSnapshots_AppointmentId",
                table: "AppointmentSnapshots",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSnapshots_OldWorkScheduleId",
                table: "AppointmentSnapshots",
                column: "OldWorkScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSnapshots_UpdatedByUserId",
                table: "AppointmentSnapshots",
                column: "UpdatedByUserId");

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
                name: "IX_MedicalReports_QueueTicketId",
                table: "MedicalReports",
                column: "QueueTicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_PersonId",
                table: "Notifications",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PersonId",
                table: "Patients",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Persons_PhoneNumber",
                table: "Persons",
                column: "PhoneNumber",
                unique: true,
                filter: "[PhoneNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTickets_AppointmentId",
                table: "QueueTickets",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
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
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PersonId",
                table: "Users",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentSnapshots");

            migrationBuilder.DropTable(
                name: "MedicalReports");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "QueueCounters");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "ShiftRequests");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "WorkHistories");

            migrationBuilder.DropTable(
                name: "QueueTickets");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Specialties");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WorkSchedules");

            migrationBuilder.DropTable(
                name: "Doctors");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "Persons");
        }
    }
}
