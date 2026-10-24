/*
  Additive, re-runnable Appointment data import.
  Copies data from ClinicDB (and patient projection) to ClinicAppointmentDB.
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

    IF DB_ID(N'ClinicAppointmentDB') IS NULL
        THROW 51001, 'Target ClinicAppointmentDB does not exist on this SQL Server.', 1;

    -- 1. Import PatientProjections
    INSERT INTO ClinicAppointmentDB.dbo.PatientProjections (Id, IdentityUserId, PersonId, FullName, PhoneNumber, Gender, DateOfBirth, UpdatedAt)
    SELECT pat.Id,
           per.IdentityUserId,
           per.Id AS PersonId,
           per.FullName,
           per.PhoneNumber,
           CASE CONVERT(int, per.Gender) WHEN 0 THEN N'Male' WHEN 1 THEN N'Female' WHEN 2 THEN N'Other' ELSE N'Unspecified' END,
           per.DateOfBirth,
           COALESCE(pat.UpdatedAt, pat.CreatedAt, GETUTCDATE())
    FROM ClinicDB.dbo.Patients AS pat
    JOIN ClinicDB.dbo.Persons AS per ON pat.PersonId = per.Id
    WHERE NOT EXISTS (SELECT 1 FROM ClinicAppointmentDB.dbo.PatientProjections AS target WHERE target.Id = pat.Id);

    -- 2. Import Appointments with Denormalized Columns
    INSERT INTO ClinicAppointmentDB.dbo.Appointments (
        Id,
        PatientId,
        PatientName,
        PatientPhone,
        PatientGender,
        PatientDateOfBirth,
        WorkScheduleId,
        DoctorId,
        DoctorName,
        SpecialtyName,
        ScheduleDate,
        TimeSlot,
        Reason,
        Status,
        IsWalkIn,
        UpdatedByUserId,
        CreatedAt,
        UpdatedAt,
        IsDeleted
    )
    SELECT 
        a.Id,
        a.PatientId,
        COALESCE(per.FullName, N'Unknown Patient'),
        per.PhoneNumber,
        CASE CONVERT(int, per.Gender) WHEN 0 THEN N'Male' WHEN 1 THEN N'Female' WHEN 2 THEN N'Other' ELSE N'Unspecified' END,
        per.DateOfBirth,
        a.WorkScheduleId,
        a.DoctorId,
        a.DoctorName,
        a.SpecialtyName,
        a.ScheduleDate,
        a.TimeSlot,
        a.Reason,
        a.Status,
        a.IsWalkIn,
        a.UpdatedByUserId,
        a.CreatedAt,
        a.UpdatedAt,
        a.IsDeleted
    FROM ClinicDB.dbo.Appointments AS a
    LEFT JOIN ClinicDB.dbo.Patients AS pat ON a.PatientId = pat.Id
    LEFT JOIN ClinicDB.dbo.Persons AS per ON pat.PersonId = per.Id
    WHERE NOT EXISTS (SELECT 1 FROM ClinicAppointmentDB.dbo.Appointments AS target WHERE target.Id = a.Id);

    -- 3. Import AppointmentSnapshots with historical traceback details
    INSERT INTO ClinicAppointmentDB.dbo.AppointmentSnapshots (
        Id,
        AppointmentId,
        OldWorkScheduleId,
        OldDoctorId,
        OldDoctorName,
        OldSpecialtyName,
        OldScheduleDate,
        TimeSlot,
        Reason,
        Status,
        UpdatedAt,
        UpdatedByUserId,
        UpdatedByUserName
    )
    SELECT 
        s.Id,
        s.AppointmentId,
        s.OldWorkScheduleId,
        COALESCE(ws.DoctorId, '00000000-0000-0000-0000-000000000000'),
        COALESCE(docPer.FullName, N''),
        NULL,
        COALESCE(ws.Date, '2000-01-01'),
        s.TimeSlot,
        s.Reason,
        s.Status,
        s.UpdatedAt,
        s.UpdatedByUserId,
        updPer.FullName
    FROM ClinicDB.dbo.AppointmentSnapshots AS s
    LEFT JOIN ClinicDB.dbo.WorkSchedules AS ws ON s.OldWorkScheduleId = ws.Id
    LEFT JOIN ClinicDB.dbo.Doctors AS doc ON ws.DoctorId = doc.Id
    LEFT JOIN ClinicDB.dbo.Employees AS emp ON doc.EmployeeId = emp.Id
    LEFT JOIN ClinicDB.dbo.Persons AS docPer ON emp.PersonId = docPer.Id
    LEFT JOIN ClinicDB.dbo.Persons AS updPer ON s.UpdatedByUserId = updPer.IdentityUserId
    WHERE NOT EXISTS (SELECT 1 FROM ClinicAppointmentDB.dbo.AppointmentSnapshots AS target WHERE target.Id = s.Id);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT 'PatientProjections' AS Entity, COUNT_BIG(*) AS ImportedCount FROM ClinicAppointmentDB.dbo.PatientProjections
UNION ALL SELECT 'Appointments', COUNT_BIG(*) FROM ClinicAppointmentDB.dbo.Appointments
UNION ALL SELECT 'AppointmentSnapshots', COUNT_BIG(*) FROM ClinicAppointmentDB.dbo.AppointmentSnapshots;
