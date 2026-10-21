/*
  Post-cutover Identity reconciliation report.
  Clinic auth tables are intentionally absent after RemoveClinicAuthEntities.
  Counts and mismatch totals only; credential hashes and tokens are never returned.
*/
SET NOCOUNT ON;

IF DB_ID(N'ClinicDB') IS NULL OR DB_ID(N'ClinicIdentityDB') IS NULL
    THROW 51001, 'ClinicDB and ClinicIdentityDB must both exist on this SQL Server.', 1;

SELECT Entity, ClinicRows, IdentityRows
FROM
(
    SELECT N'Persons linked to Identity' AS Entity,
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.Persons WHERE IsDeleted = 0 AND IdentityUserId IS NOT NULL) AS ClinicRows,
           (SELECT COUNT_BIG(*) FROM ClinicIdentityDB.dbo.Persons) AS IdentityRows
    UNION ALL SELECT N'Identity Users',
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.Persons WHERE IsDeleted = 0 AND IdentityUserId IS NOT NULL),
           (SELECT COUNT_BIG(*) FROM ClinicIdentityDB.dbo.Users)
    UNION ALL SELECT N'Identity Roles', 0, (SELECT COUNT_BIG(*) FROM ClinicIdentityDB.dbo.Roles)
    UNION ALL SELECT N'Identity Permissions', 0, (SELECT COUNT_BIG(*) FROM ClinicIdentityDB.dbo.Permissions)
    UNION ALL SELECT N'Identity UserRoles', 0, (SELECT COUNT_BIG(*) FROM ClinicIdentityDB.dbo.UserRoles)
    UNION ALL SELECT N'Identity RolePermissions', 0, (SELECT COUNT_BIG(*) FROM ClinicIdentityDB.dbo.RolePermissions)
    UNION ALL SELECT N'Identity RefreshTokens', 0, (SELECT COUNT_BIG(*) FROM ClinicIdentityDB.dbo.RefreshTokens)
) AS counts
ORDER BY Entity;

SELECT CheckName, MismatchCount
FROM
(
    SELECT N'Clinic persons with missing IdentityUserId' AS CheckName, COUNT_BIG(*) AS MismatchCount
    FROM ClinicDB.dbo.Persons AS clinicPerson
    WHERE clinicPerson.IsDeleted = 0
      AND clinicPerson.IdentityUserId IS NULL

    UNION ALL SELECT N'Clinic IdentityUserId links missing from Identity', COUNT_BIG(*)
    FROM ClinicDB.dbo.Persons AS clinicPerson
    WHERE clinicPerson.IsDeleted = 0
      AND clinicPerson.IdentityUserId IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM ClinicIdentityDB.dbo.Users AS identityUser
          WHERE identityUser.Id = clinicPerson.IdentityUserId
      )

    UNION ALL SELECT N'Identity users missing linked Identity person', COUNT_BIG(*)
    FROM ClinicIdentityDB.dbo.Users AS identityUser
    WHERE NOT EXISTS
      (
          SELECT 1
          FROM ClinicIdentityDB.dbo.Persons AS identityPerson
          WHERE identityPerson.Id = identityUser.PersonId
      )

    UNION ALL SELECT N'Identity user-role links with missing user or role', COUNT_BIG(*)
    FROM ClinicIdentityDB.dbo.UserRoles AS userRole
    WHERE NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Users AS identityUser WHERE identityUser.Id = userRole.UserId)
       OR NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Roles AS identityRole WHERE identityRole.Id = userRole.RoleId)

    UNION ALL SELECT N'Identity role-permission links with missing role or permission', COUNT_BIG(*)
    FROM ClinicIdentityDB.dbo.RolePermissions AS rolePermission
    WHERE NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Roles AS identityRole WHERE identityRole.Id = rolePermission.RoleId)
       OR NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Permissions AS identityPermission WHERE identityPermission.Id = rolePermission.PermissionId)

    UNION ALL SELECT N'Identity refresh tokens with missing user', COUNT_BIG(*)
    FROM ClinicIdentityDB.dbo.RefreshTokens AS refreshToken
    WHERE NOT EXISTS (SELECT 1 FROM ClinicIdentityDB.dbo.Users AS identityUser WHERE identityUser.Id = refreshToken.UserId)

    UNION ALL SELECT N'Inactive Identity accounts without Clinic person link', COUNT_BIG(*)
    FROM ClinicIdentityDB.dbo.Users AS identityUser
    WHERE identityUser.IsActive = 0
      AND NOT EXISTS
      (
          SELECT 1
          FROM ClinicDB.dbo.Persons AS clinicPerson
          WHERE clinicPerson.IdentityUserId = identityUser.Id
      )
) AS checks
ORDER BY CheckName;
