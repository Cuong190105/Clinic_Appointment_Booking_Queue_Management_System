/*
  Post-import Scheduling reconciliation report.
  Compares row counts and validates relational integrity between ClinicDB and ClinicSchedulingDB.
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

    UNION ALL SELECT N'Specialties mismatch count between ClinicDB and SchedulingDB',
           ABS((SELECT COUNT_BIG(*) FROM ClinicDB.dbo.Specialties WHERE IsDeleted = 0) -
               (SELECT COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.Specialties WHERE IsDeleted = 0))

    UNION ALL SELECT N'Doctors mismatch count between ClinicDB and SchedulingDB',
           ABS((SELECT COUNT_BIG(*) FROM ClinicDB.dbo.Doctors WHERE IsDeleted = 0) -
               (SELECT COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.Doctors WHERE IsDeleted = 0))

    UNION ALL SELECT N'Employees mismatch count between ClinicDB and SchedulingDB',
           ABS((SELECT COUNT_BIG(*) FROM ClinicDB.dbo.Employees WHERE IsDeleted = 0) -
               (SELECT COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.Employees WHERE IsDeleted = 0))

    UNION ALL SELECT N'WorkSchedules mismatch count between ClinicDB and SchedulingDB',
           ABS((SELECT COUNT_BIG(*) FROM ClinicDB.dbo.WorkSchedules WHERE IsDeleted = 0) -
               (SELECT COUNT_BIG(*) FROM ClinicSchedulingDB.dbo.WorkSchedules WHERE IsDeleted = 0))
) AS checks
ORDER BY CheckName;

