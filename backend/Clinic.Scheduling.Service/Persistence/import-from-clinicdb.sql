/*
    Additive, re-runnable Scheduling data import.
    Copies data from ClinicDB (and projections from ClinicIdentityDB) to ClinicSchedulingDB.
    Existing rows are reconciled before missing rows are inserted.
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

    IF DB_ID(N'ClinicSchedulingDB') IS NULL
        THROW 51001, 'Target ClinicSchedulingDB does not exist on this SQL Server.', 1;

    -- 1. Reconcile and import Specialties
    UPDATE target
    SET target.Name = source.Name,
        target.Description = source.Description,
        target.EstablishedDate = source.EstablishedDate,
        target.IsDeleted = source.IsDeleted,
        target.CreatedAt = source.CreatedAt,
        target.UpdatedAt = source.UpdatedAt
    FROM ClinicSchedulingDB.dbo.Specialties AS target
    JOIN ClinicDB.dbo.Specialties AS source ON source.Id = target.Id;

    INSERT INTO ClinicSchedulingDB.dbo.Specialties (Id, Name, Description, EstablishedDate, IsDeleted, CreatedAt, UpdatedAt)
    SELECT s.Id, s.Name, s.Description, s.EstablishedDate, s.IsDeleted, s.CreatedAt, s.UpdatedAt
    FROM ClinicDB.dbo.Specialties AS s
    WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Specialties AS target WHERE target.Id = s.Id);

    -- 2. Reconcile and import Employees with Identity Projections
    UPDATE target
    SET target.PersonId = source.PersonId,
        target.IdentityUserId = COALESCE(identityUser.Id, person.IdentityUserId),
        target.FullName = person.FullName,
        target.PhoneNumber = person.PhoneNumber,
        target.Email = person.Email,
        target.Gender = CASE CONVERT(int, person.Gender) WHEN 0 THEN N'Male' WHEN 1 THEN N'Female' WHEN 2 THEN N'Other' ELSE N'Unspecified' END,
        target.DateOfBirth = person.DateOfBirth,
        target.Address = person.Address,
        target.HireDate = source.HireDate,
        target.Status = source.Status,
        target.ManagerId = source.ManagerId,
        target.IsDeleted = source.IsDeleted,
        target.CreatedAt = source.CreatedAt,
        target.UpdatedAt = source.UpdatedAt
    FROM ClinicSchedulingDB.dbo.Employees AS target
    JOIN ClinicDB.dbo.Employees AS source ON source.Id = target.Id
    JOIN ClinicDB.dbo.Persons AS person ON source.PersonId = person.Id
    LEFT JOIN ClinicIdentityDB.dbo.Users AS identityUser ON identityUser.PersonId = person.Id;

    INSERT INTO ClinicSchedulingDB.dbo.Employees (Id, PersonId, IdentityUserId, FullName, PhoneNumber, Email, Gender, DateOfBirth, Address, HireDate, Status, ManagerId, IsDeleted, CreatedAt, UpdatedAt)
    SELECT e.Id, e.PersonId,
           COALESCE(u.Id, p.IdentityUserId),
           p.FullName,
           p.PhoneNumber,
           p.Email,
           CASE CONVERT(int, p.Gender) WHEN 0 THEN N'Male' WHEN 1 THEN N'Female' WHEN 2 THEN N'Other' ELSE N'Unspecified' END,
           p.DateOfBirth,
           p.Address,
           e.HireDate,
           e.Status,
           e.ManagerId,
           e.IsDeleted,
           e.CreatedAt,
           e.UpdatedAt
    FROM ClinicDB.dbo.Employees AS e
    JOIN ClinicDB.dbo.Persons AS p ON e.PersonId = p.Id
    LEFT JOIN ClinicIdentityDB.dbo.Users AS u ON u.PersonId = p.Id
    WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Employees AS target WHERE target.Id = e.Id);

    -- 3. Reconcile and import Doctors
    UPDATE target
    SET target.EmployeeId = source.EmployeeId,
        target.LicenseNumber = source.LicenseNumber,
        target.Qualification = source.Qualification,
        target.ExperienceYears = source.ExperienceYears,
        target.Biography = source.Biography,
        target.Status = source.Status,
        target.IsDeleted = source.IsDeleted,
        target.CreatedAt = source.CreatedAt,
        target.UpdatedAt = source.UpdatedAt
    FROM ClinicSchedulingDB.dbo.Doctors AS target
    JOIN ClinicDB.dbo.Doctors AS source ON source.Id = target.Id;

    INSERT INTO ClinicSchedulingDB.dbo.Doctors (Id, EmployeeId, LicenseNumber, Qualification, ExperienceYears, Biography, Status, IsDeleted, CreatedAt, UpdatedAt)
    SELECT d.Id, d.EmployeeId, d.LicenseNumber, d.Qualification, d.ExperienceYears, d.Biography, d.Status, d.IsDeleted, d.CreatedAt, d.UpdatedAt
    FROM ClinicDB.dbo.Doctors AS d
    WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Doctors AS target WHERE target.Id = d.Id);

    -- 4. Reconcile and import WorkHistories
    UPDATE target
    SET target.DoctorId = source.DoctorId,
        target.SpecialtyId = source.SpecialtyId,
        target.StartDate = source.StartDate,
        target.EndDate = source.EndDate,
        target.IsDeleted = source.IsDeleted,
        target.CreatedAt = source.CreatedAt,
        target.UpdatedAt = source.UpdatedAt
    FROM ClinicSchedulingDB.dbo.WorkHistories AS target
    JOIN ClinicDB.dbo.WorkHistories AS source ON source.Id = target.Id;

    INSERT INTO ClinicSchedulingDB.dbo.WorkHistories (Id, DoctorId, SpecialtyId, StartDate, EndDate, IsDeleted, CreatedAt, UpdatedAt)
    SELECT wh.Id, wh.DoctorId, wh.SpecialtyId, wh.StartDate, wh.EndDate, wh.IsDeleted, wh.CreatedAt, wh.UpdatedAt
    FROM ClinicDB.dbo.WorkHistories AS wh
    WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.WorkHistories AS target WHERE target.Id = wh.Id);

    -- 5. Reconcile and import WorkSchedules
    UPDATE target
    SET target.DoctorId = source.DoctorId,
        target.Date = source.Date,
        target.ShiftStart = source.ShiftStart,
        target.ShiftEnd = source.ShiftEnd,
        target.RecommendedCapacity = source.RecommendedCapacity,
        target.Status = source.Status,
        target.CancellationReason = source.CancellationReason,
        target.IsDeleted = source.IsDeleted,
        target.CreatedAt = source.CreatedAt,
        target.UpdatedAt = source.UpdatedAt
    FROM ClinicSchedulingDB.dbo.WorkSchedules AS target
    JOIN ClinicDB.dbo.WorkSchedules AS source ON source.Id = target.Id;

    INSERT INTO ClinicSchedulingDB.dbo.WorkSchedules (Id, DoctorId, Date, ShiftStart, ShiftEnd, RecommendedCapacity, Status, CancellationReason, IsDeleted, CreatedAt, UpdatedAt)
    SELECT ws.Id, ws.DoctorId, ws.Date, ws.ShiftStart, ws.ShiftEnd, ws.RecommendedCapacity, ws.Status, ws.CancellationReason, ws.IsDeleted, ws.CreatedAt, ws.UpdatedAt
    FROM ClinicDB.dbo.WorkSchedules AS ws
    WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.WorkSchedules AS target WHERE target.Id = ws.Id);

    -- 6. Reconcile and import ShiftRequests
    UPDATE target
    SET target.DoctorId = source.DoctorId,
        target.Date = source.Date,
        target.ShiftStart = source.ShiftStart,
        target.ShiftEnd = source.ShiftEnd,
        target.RecommendedCapacity = source.RecommendedCapacity,
        target.Reason = source.Reason,
        target.Status = source.Status,
        target.IsDeleted = source.IsDeleted,
        target.CreatedAt = source.CreatedAt,
        target.UpdatedAt = source.UpdatedAt
    FROM ClinicSchedulingDB.dbo.ShiftRequests AS target
    JOIN ClinicDB.dbo.ShiftRequests AS source ON source.Id = target.Id;

    INSERT INTO ClinicSchedulingDB.dbo.ShiftRequests (Id, DoctorId, Date, ShiftStart, ShiftEnd, RecommendedCapacity, Reason, Status, IsDeleted, CreatedAt, UpdatedAt)
    SELECT sr.Id, sr.DoctorId, sr.Date, sr.ShiftStart, sr.ShiftEnd, sr.RecommendedCapacity, sr.Reason, sr.Status, sr.IsDeleted, sr.CreatedAt, sr.UpdatedAt
    FROM ClinicDB.dbo.ShiftRequests AS sr
    WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.ShiftRequests AS target WHERE target.Id = sr.Id);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT 'Specialties' AS Entity, COUNT_BIG(*) AS ImportedCount FROM ClinicSchedulingDB.dbo.Specialties
UNION ALL SELECT 'Employees', COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.Employees
UNION ALL SELECT 'Doctors', COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.Doctors
UNION ALL SELECT 'WorkHistories', COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.WorkHistories
UNION ALL SELECT 'WorkSchedules', COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.WorkSchedules
UNION ALL SELECT 'ShiftRequests', COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.ShiftRequests;

