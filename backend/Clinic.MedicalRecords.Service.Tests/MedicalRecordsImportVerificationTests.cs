using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Clinic.MedicalRecords.Service.Entities;
using Clinic.MedicalRecords.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinic.MedicalRecords.Service.Tests;

public class MedicalRecordsImportVerificationTests
{
    [Fact]
    public void ImportAndVerifyScripts_ExistAndContainRequiredSqlDirectives()
    {
        var basePath = AppContext.BaseDirectory;
        // Navigate up from bin/Debug/net10.0 to service directory or workspace root
        var servicePersistenceDir = Path.GetFullPath(Path.Combine(basePath, "../../../../Clinic.MedicalRecords.Service/Persistence"));
        
        var importSqlPath = Path.Combine(servicePersistenceDir, "import-from-clinicdb.sql");
        var verifySqlPath = Path.Combine(servicePersistenceDir, "verify-medical-records-import.sql");

        Assert.True(File.Exists(importSqlPath), $"Expected import SQL at {importSqlPath}");
        Assert.True(File.Exists(verifySqlPath), $"Expected verify SQL at {verifySqlPath}");

        var importSql = File.ReadAllText(importSqlPath);
        Assert.Contains("ClinicDB", importSql);
        Assert.Contains("ClinicMedicalRecordsDB", importSql);
        Assert.Contains("ClinicMedicalRecordsDB.dbo.Patients", importSql);
        Assert.Contains("ClinicMedicalRecordsDB.dbo.MedicalReports", importSql);
        Assert.Contains("WHERE NOT EXISTS", importSql);
        Assert.Contains("Finalized", importSql);

        var verifySql = File.ReadAllText(verifySqlPath);
        Assert.Contains("MismatchCount", verifySql);
        Assert.Contains("Patients missing FullName", verifySql);
        Assert.Contains("MedicalReports missing Patient reference", verifySql);
        Assert.Contains("duplicate AppointmentId", verifySql);
        Assert.Contains("invalid Status", verifySql);
        Assert.Contains("Patient identity link mismatch", verifySql);
        Assert.Contains("THROW 51002", verifySql);
        Assert.DoesNotContain("Status mismatch between ClinicDB and MedicalRecordsDB", verifySql);
        Assert.Contains("PersonId", importSql);
        Assert.Contains("IdentityUserId", importSql);
    }

    [Fact]
    public async Task ImporterSimulation_CopiesPatientsAndReports_WithIdempotency()
    {
        var options = new DbContextOptionsBuilder<MedicalRecordsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new MedicalRecordsDbContext(options);

        // Seed target with empty state
        var patientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        // 1. First run simulation: Insert patient and medical report
        var patient = new Patient(
            patientId,
            identityUserId: Guid.NewGuid(),
            fullName: "Nguyen Van A",
            phoneNumber: "0901234567",
            email: "vana@example.com",
            dateOfBirth: new DateOnly(1990, 5, 10),
            gender: "Male",
            address: "123 Main St",
            insuranceNumber: "BHYT-123456",
            emergencyContact: "0909876543");

        var report = new MedicalReport(
            appointmentId,
            patientId,
            doctorId,
            doctorName: "Dr. Doctor",
            specialtyName: "Cardiology",
            scheduleDate: new DateOnly(2026, 10, 20),
            symptoms: "Chest pain",
            diagnosis: "Stable angina",
            prescription: "Aspirin 81mg",
            notes: "Follow up in 2 weeks");
        report.FinalizeReport();

        db.Patients.Add(patient);
        db.MedicalReports.Add(report);
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Patients.CountAsync());
        Assert.Equal(1, await db.MedicalReports.CountAsync());

        // 2. Second run simulation (idempotency check: WHERE NOT EXISTS)
        if (!await db.Patients.AnyAsync(p => p.Id == patientId))
        {
            db.Patients.Add(patient);
        }

        if (!await db.MedicalReports.AnyAsync(r => r.Id == report.Id))
        {
            db.MedicalReports.Add(report);
        }
        await db.SaveChangesAsync();

        // Assert counts remain exactly 1 (no duplicates created)
        Assert.Equal(1, await db.Patients.CountAsync());
        Assert.Equal(1, await db.MedicalReports.CountAsync());

        var savedReport = await db.MedicalReports.Include(r => r.Patient).FirstOrDefaultAsync(r => r.AppointmentId == appointmentId);
        Assert.NotNull(savedReport);
        Assert.Equal("Finalized", savedReport.Status);
        Assert.Equal("Nguyen Van A", savedReport.Patient!.FullName);
    }
}
