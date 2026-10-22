/*
  Additive, re-runnable Identity data import. Run only after ClinicIdentityDB migrations.
  No source tables are changed or deleted. Review row counts before application cutover.
*/
-- sqlcmd sessions may start with QUOTED_IDENTIFIER OFF. SQL Server requires
-- these settings when modifying tables with filtered/computed indexes.
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

    INSERT INTO ClinicIdentityDB.dbo.Persons (Id, FullName, PhoneNumber, Email, DateOfBirth, Gender, Address, CreatedAt)
    SELECT p.Id, p.FullName, p.PhoneNumber, p.Email, p.DateOfBirth,
           CASE CONVERT(int, p.Gender) WHEN 0 THEN N'Male' WHEN 1 THEN N'Female' WHEN 2 THEN N'Other' ELSE N'Unspecified' END,
           p.Address, p.CreatedAt
    FROM ClinicDB.dbo.Persons AS p
    WHERE p.IsDeleted = 0
      AND NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Persons AS target WHERE target.Id = p.Id);

    -- The clinic auth tables are optional during the cutover. They exist in
    -- pre-cutover databases and are absent after RemoveClinicAuthEntities.
    IF OBJECT_ID(N'ClinicDB.dbo.Roles', N'U') IS NOT NULL
    BEGIN
        INSERT INTO ClinicIdentityDB.dbo.Roles (Id, Name, Description)
        SELECT r.Id, r.Name, r.Description
        FROM ClinicDB.dbo.Roles AS r
        WHERE r.IsDeleted = 0
          AND NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Roles AS target WHERE target.Id = r.Id);
    END;

    IF OBJECT_ID(N'ClinicDB.dbo.Permissions', N'U') IS NOT NULL
    BEGIN
        INSERT INTO ClinicIdentityDB.dbo.Permissions (Id, Name, Description)
        SELECT p.Id, p.Name, p.Description
        FROM ClinicDB.dbo.Permissions AS p
        WHERE p.IsDeleted = 0
          AND NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Permissions AS target WHERE target.Id = p.Id);
    END;

    IF OBJECT_ID(N'ClinicDB.dbo.Patients', N'U') IS NOT NULL
    BEGIN
        INSERT INTO ClinicDB.dbo.Patients (Id, PersonId, InsuranceNumber, EmergencyContact, CreatedAt, IsDeleted)
        SELECT 
            NEWID() AS Id,
            p.Id AS PersonId,
            CONCAT('BHXH-STAFF-', SUBSTRING(CONVERT(NVARCHAR(36), p.Id), 1, 8)) AS InsuranceNumber,
            p.PhoneNumber AS EmergencyContact,
            GETUTCDATE() AS CreatedAt,
            0 AS IsDeleted
        FROM ClinicDB.dbo.Persons p
        WHERE NOT EXISTS (
            SELECT 1 FROM ClinicDB.dbo.Patients pt WHERE pt.PersonId = p.Id AND pt.IsDeleted = 0
        )
        AND (
            EXISTS (SELECT 1 FROM ClinicDB.dbo.Employees e WHERE e.PersonId = p.Id AND e.IsDeleted = 0)
            OR EXISTS (SELECT 1 FROM ClinicDB.dbo.Users u WHERE u.PersonId = p.Id AND u.IsDeleted = 0)
        );
    END;

    IF OBJECT_ID(N'ClinicDB.dbo.Users', N'U') IS NOT NULL
    BEGIN
        INSERT INTO ClinicIdentityDB.dbo.Users (Id, PersonId, PatientId, EmployeeId, DoctorId, Username, PasswordHash, IsActive, CreatedAt)
        SELECT u.Id, u.PersonId, patient.Id, employee.Id, doctor.Id, u.Username, u.PasswordHash, u.IsActive, u.CreatedAt
        FROM ClinicDB.dbo.Users AS u
        LEFT JOIN ClinicDB.dbo.Patients AS patient ON patient.PersonId = u.PersonId AND patient.IsDeleted = 0
        LEFT JOIN ClinicDB.dbo.Employees AS employee ON employee.PersonId = u.PersonId AND employee.IsDeleted = 0
        LEFT JOIN ClinicDB.dbo.Doctors AS doctor ON doctor.EmployeeId = employee.Id AND doctor.IsDeleted = 0
        WHERE u.IsDeleted = 0
          AND NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Users AS target WHERE target.Id = u.Id);
    END;

    IF OBJECT_ID(N'ClinicDB.dbo.UserRoles', N'U') IS NOT NULL
    BEGIN
        INSERT INTO ClinicIdentityDB.dbo.UserRoles (UserId, RoleId)
        SELECT ur.UserId, ur.RoleId
        FROM ClinicDB.dbo.UserRoles AS ur
        WHERE EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Users AS u WHERE u.Id = ur.UserId)
          AND EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Roles AS r WHERE r.Id = ur.RoleId)
          AND NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.UserRoles AS target WHERE target.UserId = ur.UserId AND target.RoleId = ur.RoleId);
    END;

    IF OBJECT_ID(N'ClinicDB.dbo.RolePermissions', N'U') IS NOT NULL
    BEGIN
        INSERT INTO ClinicIdentityDB.dbo.RolePermissions (RoleId, PermissionId)
        SELECT rp.RoleId, rp.PermissionId
        FROM ClinicDB.dbo.RolePermissions AS rp
        WHERE EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Roles AS r WHERE r.Id = rp.RoleId)
          AND EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Permissions AS p WHERE p.Id = rp.PermissionId)
          AND NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.RolePermissions AS target WHERE target.RoleId = rp.RoleId AND target.PermissionId = rp.PermissionId);
    END;

    IF OBJECT_ID(N'ClinicDB.dbo.RefreshTokens', N'U') IS NOT NULL
    BEGIN
        INSERT INTO ClinicIdentityDB.dbo.RefreshTokens (Id, UserId, TokenHash, IssuedAt, ExpiresAt, RevokedAt)
        SELECT rt.Id, rt.UserId, rt.TokenHash, rt.IssuedAt, rt.ExpiresAt, rt.RevokedAt
        FROM ClinicDB.dbo.RefreshTokens AS rt
        WHERE EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Users AS u WHERE u.Id = rt.UserId)
          AND NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.RefreshTokens AS target WHERE target.Id = rt.Id);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT 'Persons' AS Entity, COUNT_BIG(*) AS ImportedCount FROM ClinicIdentityDB.dbo.Persons
UNION ALL SELECT 'Users', COUNT_BIG(*) FROM ClinicIdentityDB.dbo.Users
UNION ALL SELECT 'Roles', COUNT_BIG(*) FROM ClinicIdentityDB.dbo.Roles
UNION ALL SELECT 'Permissions', COUNT_BIG(*) FROM ClinicIdentityDB.dbo.Permissions
UNION ALL SELECT 'UserRoles', COUNT_BIG(*) FROM ClinicIdentityDB.dbo.UserRoles
UNION ALL SELECT 'RolePermissions', COUNT_BIG(*) FROM ClinicIdentityDB.dbo.RolePermissions
UNION ALL SELECT 'RefreshTokens', COUNT_BIG(*) FROM ClinicIdentityDB.dbo.RefreshTokens;
