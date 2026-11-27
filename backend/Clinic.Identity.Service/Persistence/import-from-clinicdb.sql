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
          -- Identity seeds its roles on startup (before this import runs); match by name as well as id
          -- so a name already present under another id is not inserted twice (unique index on Name).
          AND NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Roles AS target WHERE target.Id = r.Id OR target.Name = r.Name);
    END;

    IF OBJECT_ID(N'ClinicDB.dbo.Permissions', N'U') IS NOT NULL
    BEGIN
        INSERT INTO ClinicIdentityDB.dbo.Permissions (Id, Name, Description)
        SELECT p.Id, p.Name, p.Description
        FROM ClinicDB.dbo.Permissions AS p
        WHERE p.IsDeleted = 0
          -- Identity's startup seed creates queue permissions with its own ids, so the monolith row for
          -- the same name (e.g. queue.view) must be skipped, not inserted (unique index on Name).
          AND NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Permissions AS target WHERE target.Id = p.Id OR target.Name = p.Name);
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
            OR EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Users u WHERE u.PersonId = p.Id AND u.IsActive = 1)
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
        -- Resolve roles by name: the target id can differ from the monolith id (see Roles above).
        INSERT INTO ClinicIdentityDB.dbo.UserRoles (UserId, RoleId)
        SELECT DISTINCT ur.UserId, targetRole.Id
        FROM ClinicDB.dbo.UserRoles AS ur
        JOIN ClinicDB.dbo.Roles AS sourceRole ON sourceRole.Id = ur.RoleId
        JOIN ClinicIdentityDB.dbo.Roles AS targetRole ON targetRole.Name = sourceRole.Name
        WHERE EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Users AS u WHERE u.Id = ur.UserId)
          AND NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.UserRoles AS target WHERE target.UserId = ur.UserId AND target.RoleId = targetRole.Id);
    END;

    IF OBJECT_ID(N'ClinicDB.dbo.RolePermissions', N'U') IS NOT NULL
    BEGIN
        -- Resolve both sides by name. Queue permissions are excluded here: they are owned by
        -- QueuePermissionCatalog and applied by the queue-permission-map block below.
        INSERT INTO ClinicIdentityDB.dbo.RolePermissions (RoleId, PermissionId)
        SELECT DISTINCT targetRole.Id, targetPermission.Id
        FROM ClinicDB.dbo.RolePermissions AS rp
        JOIN ClinicDB.dbo.Roles AS sourceRole ON sourceRole.Id = rp.RoleId
        JOIN ClinicDB.dbo.Permissions AS sourcePermission ON sourcePermission.Id = rp.PermissionId
        JOIN ClinicIdentityDB.dbo.Roles AS targetRole ON targetRole.Name = sourceRole.Name
        JOIN ClinicIdentityDB.dbo.Permissions AS targetPermission ON targetPermission.Name = sourcePermission.Name
        WHERE sourcePermission.Name NOT LIKE N'queue.%' AND sourcePermission.Name <> N'doctor.queue.view'
          AND NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.RolePermissions AS target WHERE target.RoleId = targetRole.Id AND target.PermissionId = targetPermission.Id);
    END;

    -- BEGIN queue-permission-map
    -- Mirrors Clinic.Identity.Service/Auth/QueuePermissionCatalog.cs (checked by QueuePermissionCatalogTests).
    -- Identity reconciles the same grants on startup; applying them here keeps a freshly imported
    -- database consistent without an Identity restart.
    DECLARE @QueueGrants TABLE (RoleName nvarchar(50) NOT NULL, PermissionName nvarchar(150) NOT NULL);
    INSERT INTO @QueueGrants (RoleName, PermissionName) VALUES
        (N'Admin', N'queue.view'),
        (N'Admin', N'doctor.queue.view'),
        (N'Admin', N'queue.check-in'),
        (N'Admin', N'queue.call-next'),
        (N'Admin', N'queue.skip'),
        (N'Admin', N'queue.skip-waiting'),
        (N'Admin', N'queue.retry'),
        (N'Admin', N'queue.priority'),
        (N'Admin', N'queue.start-exam.own'),
        (N'Admin', N'queue.start-exam.any'),
        (N'Admin', N'queue.complete-exam.own'),
        (N'Admin', N'queue.complete-exam.any'),
        (N'Receptionist', N'queue.view'),
        (N'Receptionist', N'queue.check-in'),
        (N'Receptionist', N'queue.call-next'),
        (N'Receptionist', N'queue.skip-waiting'),
        (N'Receptionist', N'queue.retry'),
        (N'Receptionist', N'queue.priority'),
        (N'Doctor', N'doctor.queue.view'),
        (N'Doctor', N'queue.call-next'),
        (N'Doctor', N'queue.skip'),
        (N'Doctor', N'queue.start-exam.own'),
        (N'Doctor', N'queue.complete-exam.own');
    -- END queue-permission-map

    INSERT INTO ClinicIdentityDB.dbo.Permissions (Id, Name, Description)
    SELECT NEWID(), names.PermissionName, names.PermissionName
    FROM (SELECT DISTINCT PermissionName FROM @QueueGrants) AS names
    WHERE NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Permissions AS p WHERE p.Name = names.PermissionName);

    DELETE rp
    FROM ClinicIdentityDB.dbo.RolePermissions AS rp
    JOIN ClinicIdentityDB.dbo.Roles AS r ON r.Id = rp.RoleId
    JOIN ClinicIdentityDB.dbo.Permissions AS p ON p.Id = rp.PermissionId
    WHERE p.Name IN (SELECT PermissionName FROM @QueueGrants)
      AND r.Name IN (SELECT RoleName FROM @QueueGrants)
      AND NOT EXISTS (SELECT 1 FROM @QueueGrants AS g WHERE g.RoleName = r.Name AND g.PermissionName = p.Name);

    INSERT INTO ClinicIdentityDB.dbo.RolePermissions (RoleId, PermissionId)
    SELECT r.Id, p.Id
    FROM @QueueGrants AS g
    JOIN ClinicIdentityDB.dbo.Roles AS r ON r.Name = g.RoleName
    JOIN ClinicIdentityDB.dbo.Permissions AS p ON p.Name = g.PermissionName
    WHERE NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.RolePermissions AS target WHERE target.RoleId = r.Id AND target.PermissionId = p.Id);

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
