using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class DatabaseInitializer
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IUserService _userService;
        private readonly IRoleRepository _roleRepository;
        private readonly IPersonService _personService;
        
        public DatabaseInitializer(ApplicationDbContext context, IConfiguration configuration, IUserService userService, IRoleRepository roleRepository, IPersonService personService)
        {
            _context = context;
            _configuration = configuration;
            _userService = userService;
            _roleRepository = roleRepository;
            _personService = personService;
        }
        public async Task CreateInitialAdminAsync(string? username = null, string? password = null)
        {
            var adminUsername = username ?? _configuration["InitialAdmin:Username"] ?? "admin";
            var adminPassword = password ?? _configuration["InitialAdmin:Password"] ?? "AdminPassword123!";

            if (await _context.Users.AnyAsync(u => u.Username == adminUsername))
            {
                return;
            }

            var person = await _personService.CreateOrGetPersonAsync(
                "System Administrator",
                _configuration["InitialAdmin:PhoneNumber"] ?? "0111111111",
                _configuration["InitialAdmin:Email"] ?? "admin@clinic.local",
                new DateOnly(1990, 1, 1),
                Domain.Enums.Gender.Male,
                "Ho Chi Minh City");

            var user = await _userService.CreateUserAsync(adminUsername, adminPassword, person);
            var employee = new Employee(person, new DateOnly(2025, 1, 1));

            var adminRole = await _roleRepository.GetByNameAsync("Admin");
            if (adminRole == null)
            {
                throw new InvalidOperationException("Admin role not found");
            }
            user.AssignRole(adminRole);
            await _context.AddAsync(employee);
            await _context.SaveChangesAsync();
        }

        public async Task MigrateAsync()
        {
            await _context.Database.MigrateAsync();

            const string patchSql = @"
                IF COL_LENGTH('WorkSchedules', 'PatientLimit') IS NOT NULL AND COL_LENGTH('WorkSchedules', 'RecommendedCapacity') IS NULL
                BEGIN
                    EXEC sp_rename 'WorkSchedules.PatientLimit', 'RecommendedCapacity', 'COLUMN';
                END

                IF COL_LENGTH('Appointments', 'IdempotencyKey') IS NULL
                BEGIN
                    ALTER TABLE Appointments ADD IdempotencyKey NVARCHAR(100) NULL;
                END

                IF COL_LENGTH('QueueTickets', 'QueueDate') IS NULL
                BEGIN
                    ALTER TABLE QueueTickets ADD QueueDate DATE NULL;
                END

                IF COL_LENGTH('QueueTickets', 'PatientName') IS NULL
                BEGIN
                    ALTER TABLE QueueTickets ADD PatientName NVARCHAR(200) NULL;
                END

                IF COL_LENGTH('QueueTickets', 'DoctorName') IS NULL
                BEGIN
                    ALTER TABLE QueueTickets ADD DoctorName NVARCHAR(200) NULL;
                END

                IF COL_LENGTH('MedicalReports', 'AppointmentId') IS NULL
                BEGIN
                    ALTER TABLE MedicalReports ADD AppointmentId UNIQUEIDENTIFIER NULL;
                    EXEC('UPDATE m SET m.AppointmentId = q.AppointmentId FROM MedicalReports m JOIN QueueTickets q ON m.QueueTicketId = q.Id WHERE m.AppointmentId IS NULL');
                END

                IF OBJECT_ID('MedicalReportAmendments', 'U') IS NULL
                BEGIN
                    CREATE TABLE MedicalReportAmendments (
                        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        MedicalReportId UNIQUEIDENTIFIER NOT NULL,
                        Reason NVARCHAR(500) NOT NULL,
                        Content NVARCHAR(2000) NOT NULL,
                        CreatedByDoctorId UNIQUEIDENTIFIER NOT NULL,
                        DoctorName NVARCHAR(200) NOT NULL,
                        CreatedAt DATETIME2 NOT NULL,
                        UpdatedAt DATETIME2 NULL,
                        IsDeleted BIT NOT NULL DEFAULT 0,
                        CONSTRAINT FK_MedicalReportAmendments_MedicalReports FOREIGN KEY (MedicalReportId) REFERENCES MedicalReports(Id) ON DELETE CASCADE
                    );
                END

                IF OBJECT_ID('MedicalAccessAudits', 'U') IS NULL
                BEGIN
                    CREATE TABLE MedicalAccessAudits (
                        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        MedicalReportId UNIQUEIDENTIFIER NOT NULL,
                        UserId UNIQUEIDENTIFIER NOT NULL,
                        Action NVARCHAR(50) NOT NULL,
                        Details NVARCHAR(1000) NULL,
                        Timestamp DATETIME2 NOT NULL,
                        CreatedAt DATETIME2 NOT NULL,
                        UpdatedAt DATETIME2 NULL,
                        IsDeleted BIT NOT NULL DEFAULT 0
                    );
                END

                IF COL_LENGTH('Notifications', 'DeliveryAttempts') IS NULL
                BEGIN
                    ALTER TABLE Notifications ADD DeliveryAttempts INT NOT NULL DEFAULT 0;
                END

                IF COL_LENGTH('Notifications', 'FailureReason') IS NULL
                BEGIN
                    ALTER TABLE Notifications ADD FailureReason NVARCHAR(500) NULL;
                END

                IF COL_LENGTH('Notifications', 'IdempotencyKey') IS NULL
                BEGIN
                    ALTER TABLE Notifications ADD IdempotencyKey NVARCHAR(100) NULL;
                END

                IF OBJECT_ID('Patients', 'U') IS NOT NULL
                BEGIN
                    INSERT INTO Patients (Id, PersonId, InsuranceNumber, EmergencyContact, CreatedAt, UpdatedAt, IsDeleted)
                    SELECT NEWID(), p.Id, 'BHXH-' + SUBSTRING(CONVERT(NVARCHAR(36), p.Id), 1, 6), p.PhoneNumber, GETUTCDATE(), GETUTCDATE(), 0
                    FROM Persons p
                    JOIN Users u ON u.PersonId = p.Id
                    JOIN UserRoles ur ON ur.UserId = u.Id
                    JOIN Roles r ON r.Id = ur.RoleId AND r.Name = 'Patient'
                    WHERE NOT EXISTS (SELECT 1 FROM Patients pt WHERE pt.PersonId = p.Id);
                END

                IF OBJECT_ID('RolePermissions', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = '00000000-0000-0000-0000-000000000004' AND PermissionId = '20000000-0000-0000-0000-000000000008')
                    BEGIN
                        INSERT INTO RolePermissions (RoleId, PermissionId) VALUES ('00000000-0000-0000-0000-000000000004', '20000000-0000-0000-0000-000000000008');
                    END
                END
            ";

            try
            {
                await _context.Database.ExecuteSqlRawAsync(patchSql);
            }
            catch
            {
                // Best-effort in-memory / relational compatibility
            }
        }
    }
}
