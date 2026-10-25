/*
  Post-import Scheduling reconciliation report.
       Compares row counts and validates relational integrity and projections between
       ClinicDB, ClinicSchedulingDB, and ClinicIdentityDB.
  All MismatchCount values should be 0.
*/
SET NOCOUNT ON;

IF DB_ID(N'ClinicDB') IS NULL OR DB_ID(N'ClinicSchedulingDB') IS NULL
    THROW 51001, 'ClinicDB and ClinicSchedulingDB must both exist on this SQL Server.', 1;

-- 1. Row count comparison
SELECT Entity, ClinicDbRows, SchedulingDbRows
FROM
(
    SELECT N'Specialties' AS Entity,
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.Specialties WHERE IsDeleted = 0) AS ClinicDbRows,
           (SELECT COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.Specialties WHERE IsDeleted = 0) AS SchedulingDbRows
    UNION ALL SELECT N'Employees',
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.Employees WHERE IsDeleted = 0),
           (SELECT COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.Employees WHERE IsDeleted = 0)
    UNION ALL SELECT N'Doctors',
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.Doctors WHERE IsDeleted = 0),
           (SELECT COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.Doctors WHERE IsDeleted = 0)
    UNION ALL SELECT N'WorkHistories',
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.WorkHistories WHERE IsDeleted = 0),
           (SELECT COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.WorkHistories WHERE IsDeleted = 0)
    UNION ALL SELECT N'WorkSchedules',
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.WorkSchedules WHERE IsDeleted = 0),
           (SELECT COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.WorkSchedules WHERE IsDeleted = 0)
    UNION ALL SELECT N'ShiftRequests',
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.ShiftRequests WHERE IsDeleted = 0),
           (SELECT COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.ShiftRequests WHERE IsDeleted = 0)
) AS counts
ORDER BY Entity;

-- 2. Data integrity and orphan checks
SELECT CheckName, MismatchCount
FROM
(
    SELECT N'Doctors missing linked Employee in SchedulingDB' AS CheckName, COUNT_BIG(*) AS MismatchCount
    FROM ClinicSchedulingDB.dbo.Doctors AS d
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM ClinicSchedulingDB.dbo.Employees AS e
        WHERE e.Id = d.EmployeeId
    )

    UNION ALL SELECT N'Employees with missing Person projection (Empty FullName)', COUNT_BIG(*)
    FROM ClinicSchedulingDB.dbo.Employees AS e
    WHERE e.FullName IS NULL OR LTRIM(RTRIM(e.FullName)) = ''

    UNION ALL SELECT N'WorkHistories with missing Doctor or Specialty in SchedulingDB', COUNT_BIG(*)
    FROM ClinicSchedulingDB.dbo.WorkHistories AS wh
    WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Doctors AS d WHERE d.Id = wh.DoctorId)
       OR NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Specialties AS s WHERE s.Id = wh.SpecialtyId)

    UNION ALL SELECT N'WorkSchedules with missing Doctor in SchedulingDB', COUNT_BIG(*)
    FROM ClinicSchedulingDB.dbo.WorkSchedules AS ws
    WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Doctors AS d WHERE d.Id = ws.DoctorId)

    UNION ALL SELECT N'ShiftRequests with missing Doctor in SchedulingDB', COUNT_BIG(*)
    FROM ClinicSchedulingDB.dbo.ShiftRequests AS sr
    WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Doctors AS d WHERE d.Id = sr.DoctorId)

              UNION ALL SELECT N'Specialties source rows missing in SchedulingDB', COUNT_BIG(*)
              FROM ClinicDB.dbo.Specialties AS source
              WHERE source.IsDeleted = 0
                     AND NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Specialties AS target WHERE target.Id = source.Id)

              UNION ALL SELECT N'Doctors source rows missing in SchedulingDB', COUNT_BIG(*)
              FROM ClinicDB.dbo.Doctors AS source
              WHERE source.IsDeleted = 0
                     AND NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Doctors AS target WHERE target.Id = source.Id)

              UNION ALL SELECT N'Employees source rows missing in SchedulingDB', COUNT_BIG(*)
              FROM ClinicDB.dbo.Employees AS source
              WHERE source.IsDeleted = 0
                     AND NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Employees AS target WHERE target.Id = source.Id)

              UNION ALL SELECT N'WorkSchedules source rows missing in SchedulingDB', COUNT_BIG(*)
              FROM ClinicDB.dbo.WorkSchedules AS source
              WHERE source.IsDeleted = 0
                     AND NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.WorkSchedules AS target WHERE target.Id = source.Id)

              UNION ALL SELECT N'WorkHistories source rows missing in SchedulingDB', COUNT_BIG(*)
              FROM ClinicDB.dbo.WorkHistories AS source
              WHERE source.IsDeleted = 0
                     AND NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.WorkHistories AS target WHERE target.Id = source.Id)

              UNION ALL SELECT N'ShiftRequests source rows missing in SchedulingDB', COUNT_BIG(*)
              FROM ClinicDB.dbo.ShiftRequests AS source
              WHERE source.IsDeleted = 0
                     AND NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.ShiftRequests AS target WHERE target.Id = source.Id)

              UNION ALL SELECT N'Specialties missing source ID in SchedulingDB', COUNT_BIG(*)
              FROM ClinicDB.dbo.Specialties AS source
              WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Specialties AS target WHERE target.Id = source.Id)

              UNION ALL SELECT N'Employees missing source ID in SchedulingDB', COUNT_BIG(*)
              FROM ClinicDB.dbo.Employees AS source
              WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Employees AS target WHERE target.Id = source.Id)

              UNION ALL SELECT N'Doctors missing source ID in SchedulingDB', COUNT_BIG(*)
              FROM ClinicDB.dbo.Doctors AS source
              WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Doctors AS target WHERE target.Id = source.Id)

              UNION ALL SELECT N'WorkHistories missing source ID in SchedulingDB', COUNT_BIG(*)
              FROM ClinicDB.dbo.WorkHistories AS source
              WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.WorkHistories AS target WHERE target.Id = source.Id)

              UNION ALL SELECT N'WorkSchedules missing source ID in SchedulingDB', COUNT_BIG(*)
              FROM ClinicDB.dbo.WorkSchedules AS source
              WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.WorkSchedules AS target WHERE target.Id = source.Id)

              UNION ALL SELECT N'ShiftRequests missing source ID in SchedulingDB', COUNT_BIG(*)
              FROM ClinicDB.dbo.ShiftRequests AS source
              WHERE NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.ShiftRequests AS target WHERE target.Id = source.Id)

              UNION ALL SELECT N'Employee projection mismatch', COUNT_BIG(*)
              FROM ClinicDB.dbo.Employees AS source
              JOIN ClinicDB.dbo.Persons AS person ON person.Id = source.PersonId
              LEFT JOIN ClinicIdentityDB.dbo.Users AS identityUser ON identityUser.PersonId = person.Id
              JOIN ClinicSchedulingDB.dbo.Employees AS target ON target.Id = source.Id
              WHERE target.PersonId <> source.PersonId
                      OR ISNULL(target.IdentityUserId, '00000000-0000-0000-0000-000000000000') <> ISNULL(COALESCE(identityUser.Id, person.IdentityUserId), '00000000-0000-0000-0000-000000000000')
                      OR ISNULL(target.FullName, N'') <> ISNULL(person.FullName, N'')
                      OR ISNULL(target.PhoneNumber, N'') <> ISNULL(person.PhoneNumber, N'')
                      OR ISNULL(target.Email, N'') <> ISNULL(person.Email, N'')
                      OR ISNULL(target.DateOfBirth, CONVERT(date, '19000101')) <> ISNULL(person.DateOfBirth, CONVERT(date, '19000101'))
                      OR ISNULL(target.Address, N'') <> ISNULL(person.Address, N'')

              UNION ALL SELECT N'Identity users with missing Employee link in SchedulingDB', COUNT_BIG(*)
              FROM ClinicIdentityDB.dbo.Users AS identityUser
              WHERE identityUser.EmployeeId IS NOT NULL
                     AND NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Employees AS employee WHERE employee.Id = identityUser.EmployeeId)

              UNION ALL SELECT N'Identity users with missing Doctor link in SchedulingDB', COUNT_BIG(*)
              FROM ClinicIdentityDB.dbo.Users AS identityUser
              WHERE identityUser.DoctorId IS NOT NULL
                     AND NOT EXISTS (SELECT 1 FROM ClinicSchedulingDB.dbo.Doctors AS doctor WHERE doctor.Id = identityUser.DoctorId)
) AS checks
ORDER BY CheckName;

-- Target-only rows are retained because Scheduling may have accepted newer
-- records after the legacy source snapshot. They require review, not deletion.
SELECT Entity, TargetOnlyRows
FROM
(
       SELECT N'Specialties' AS Entity, COUNT_BIG(*) AS TargetOnlyRows
       FROM ClinicSchedulingDB.dbo.Specialties AS target
       WHERE NOT EXISTS (SELECT 1 FROM ClinicDB.dbo.Specialties AS source WHERE source.Id = target.Id)
       UNION ALL SELECT N'Employees', COUNT_BIG(*)
       FROM ClinicSchedulingDB.dbo.Employees AS target
       WHERE NOT EXISTS (SELECT 1 FROM ClinicDB.dbo.Employees AS source WHERE source.Id = target.Id)
       UNION ALL SELECT N'Doctors', COUNT_BIG(*)
       FROM ClinicSchedulingDB.dbo.Doctors AS target
       WHERE NOT EXISTS (SELECT 1 FROM ClinicDB.dbo.Doctors AS source WHERE source.Id = target.Id)
       UNION ALL SELECT N'WorkHistories', COUNT_BIG(*)
       FROM ClinicSchedulingDB.dbo.WorkHistories AS target
       WHERE NOT EXISTS (SELECT 1 FROM ClinicDB.dbo.WorkHistories AS source WHERE source.Id = target.Id)
       UNION ALL SELECT N'WorkSchedules', COUNT_BIG(*)
       FROM ClinicSchedulingDB.dbo.WorkSchedules AS target
       WHERE NOT EXISTS (SELECT 1 FROM ClinicDB.dbo.WorkSchedules AS source WHERE source.Id = target.Id)
       UNION ALL SELECT N'ShiftRequests', COUNT_BIG(*)
       FROM ClinicSchedulingDB.dbo.ShiftRequests AS target
       WHERE NOT EXISTS (SELECT 1 FROM ClinicDB.dbo.ShiftRequests AS source WHERE source.Id = target.Id)
) AS targetOnly
ORDER BY Entity;

-- 3. Exact source-to-target projection checks for identity links.
SELECT N'Identity users with mismatched Employee projection' AS CheckName, COUNT_BIG(*) AS MismatchCount
FROM ClinicIdentityDB.dbo.Users AS identityUser
JOIN ClinicDB.dbo.Employees AS source ON source.Id = identityUser.EmployeeId
JOIN ClinicSchedulingDB.dbo.Employees AS target ON target.Id = identityUser.EmployeeId
WHERE identityUser.EmployeeId IS NOT NULL
       AND (target.PersonId <> source.PersonId OR target.IdentityUserId <> identityUser.Id);

SELECT N'Identity users with mismatched Doctor projection' AS CheckName, COUNT_BIG(*) AS MismatchCount
FROM ClinicIdentityDB.dbo.Users AS identityUser
JOIN ClinicDB.dbo.Doctors AS source ON source.Id = identityUser.DoctorId
JOIN ClinicSchedulingDB.dbo.Doctors AS target ON target.Id = identityUser.DoctorId
WHERE identityUser.DoctorId IS NOT NULL
       AND target.EmployeeId <> source.EmployeeId;

-- 4. Snapshot completeness before legacy Appointment tables are removed.
IF OBJECT_ID(N'ClinicDB.dbo.Appointments', N'U') IS NOT NULL
BEGIN
              SELECT N'Appointments with incomplete scheduling snapshot' AS CheckName, COUNT_BIG(*) AS MismatchCount
              FROM ClinicDB.dbo.Appointments AS appointment
              WHERE appointment.DoctorId = '00000000-0000-0000-0000-000000000000'
                      OR NULLIF(LTRIM(RTRIM(appointment.DoctorName)), N'') IS NULL
                      OR appointment.ScheduleDate = CONVERT(date, '00010101');
END;

