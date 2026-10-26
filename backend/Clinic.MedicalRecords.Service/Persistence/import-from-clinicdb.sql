/*
  Additive, re-runnable Medical Records data import.
  Copies Patients (denormalizing ClinicDB.dbo.Patients + ClinicDB.dbo.Persons)
  and MedicalReports from ClinicDB to ClinicMedicalRecordsDB.
  Safe to run multiple times (idempotent).
*/
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF DB_ID(N'ClinicDB') IS NULL
        THROW 51000, 'Source ClinicDB does not exist on this SQL Server.', 1;

    IF DB_ID(N'ClinicMedicalRecordsDB') IS NULL
        THROW 51001, 'Target ClinicMedicalRecordsDB does not exist on this SQL Server.', 1;

    -- Pre-import verification: Fail if any MedicalReport has a PatientId not present in ClinicDB.dbo.Patients
    IF EXISTS (
        SELECT 1 
        FROM ClinicDB.dbo.MedicalReports mr 
        WHERE NOT EXISTS (SELECT 1 FROM ClinicDB.dbo.Patients p WHERE p.Id = mr.PatientId)
    )
        THROW 51002, 'Pre-import validation failed: Found MedicalReports referencing missing Patients in ClinicDB.', 1;

    -- 1. Import Patients (denormalizing Person data)
    INSERT INTO ClinicMedicalRecordsDB.dbo.Patients (
        Id,
        IdentityUserId,
        FullName,
        PhoneNumber,
        Email,
        DateOfBirth,
        Gender,
        Address,
        InsuranceNumber,
        EmergencyContact,
        CreatedAt,
        UpdatedAt,
        IsDeleted
    )
    SELECT 
        pat.Id,
        per.IdentityUserId,
        COALESCE(per.FullName, N'Unknown Patient'),
        per.PhoneNumber,
        per.Email,
        COALESCE(per.DateOfBirth, '1900-01-01'),
        CASE CONVERT(int, per.Gender) 
            WHEN 0 THEN N'Male' 
            WHEN 1 THEN N'Female' 
            WHEN 2 THEN N'Other' 
            ELSE N'Other' 
        END,
        per.Address,
        pat.InsuranceNumber,
        pat.EmergencyContact,
        COALESCE(pat.CreatedAt, per.CreatedAt, GETUTCDATE()),
        COALESCE(pat.UpdatedAt, per.UpdatedAt),
        pat.IsDeleted
    FROM ClinicDB.dbo.Patients AS pat
    JOIN ClinicDB.dbo.Persons AS per ON pat.PersonId = per.Id
    WHERE NOT EXISTS (
        SELECT 1 FROM ClinicMedicalRecordsDB.dbo.Patients AS target WHERE target.Id = pat.Id
    );

    -- 2. Import MedicalReports
    INSERT INTO ClinicMedicalRecordsDB.dbo.MedicalReports (
        Id,
        AppointmentId,
        PatientId,
        DoctorId,
        DoctorName,
        SpecialtyName,
        ScheduleDate,
        ExamStartTime,
        ExamEndTime,
        Symptoms,
        Diagnosis,
        Prescription,
        Notes,
        Status,
        CreatedAt,
        UpdatedAt,
        IsDeleted
    )
    SELECT 
        mr.Id,
        mr.AppointmentId,
        mr.PatientId,
        COALESCE(mr.DoctorId, app.DoctorId, '00000000-0000-0000-0000-000000000000'),
        COALESCE(mr.DoctorName, app.DoctorName, N''),
        COALESCE(mr.SpecialtyName, app.SpecialtyName, N''),
        COALESCE(mr.ScheduleDate, app.ScheduleDate),
        mr.ExamStartTime,
        mr.ExamEndTime,
        mr.Symptoms,
        mr.Diagnosis,
        mr.Prescription,
        mr.Notes,
        CASE 
            WHEN mr.Status IN (N'Finalized', N'1') THEN N'Finalized'
            ELSE N'Draft'
        END,
        COALESCE(mr.CreatedAt, GETUTCDATE()),
        mr.UpdatedAt,
        mr.IsDeleted
    FROM ClinicDB.dbo.MedicalReports AS mr
    LEFT JOIN ClinicAppointmentDB.dbo.Appointments AS app ON mr.AppointmentId = app.Id
    WHERE NOT EXISTS (
        SELECT 1 FROM ClinicMedicalRecordsDB.dbo.MedicalReports AS target WHERE target.Id = mr.Id
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT 'Patients' AS Entity, COUNT_BIG(*) AS ImportedCount FROM ClinicMedicalRecordsDB.dbo.Patients
UNION ALL SELECT 'MedicalReports', COUNT_BIG(*) FROM ClinicMedicalRecordsDB.dbo.MedicalReports;
