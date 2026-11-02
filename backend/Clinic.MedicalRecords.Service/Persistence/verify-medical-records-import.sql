/*
  Post-import Medical Records reconciliation report.
  Compares source coverage and validates relational integrity between ClinicDB and ClinicMedicalRecordsDB.
  Target-owned mutable fields (such as report Status) are not compared with the legacy source after cutover.
  All MismatchCount values should be 0.
*/
SET NOCOUNT ON;

IF DB_ID(N'ClinicDB') IS NULL OR DB_ID(N'ClinicMedicalRecordsDB') IS NULL
    THROW 51001, 'ClinicDB and ClinicMedicalRecordsDB must both exist on this SQL Server.', 1;

-- 1. Row count comparison
SELECT Entity, ClinicDbRows, MedicalRecordsDbRows
FROM
(
    SELECT N'Patients' AS Entity,
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.Patients) AS ClinicDbRows,
           (SELECT COUNT_BIG(*) FROM ClinicMedicalRecordsDB.dbo.Patients) AS MedicalRecordsDbRows
    UNION ALL SELECT N'MedicalReports',
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.MedicalReports) AS ClinicDbRows,
           (SELECT COUNT_BIG(*) FROM ClinicMedicalRecordsDB.dbo.MedicalReports) AS MedicalRecordsDbRows
) AS counts
ORDER BY Entity;

-- 2. Data integrity, relational checks, and mismatch validations
DECLARE @Checks TABLE (CheckName nvarchar(200) NOT NULL, MismatchCount bigint NOT NULL);
INSERT INTO @Checks (CheckName, MismatchCount)
SELECT CheckName, MismatchCount FROM
(
    SELECT N'Source Patients missing from target' AS CheckName, COUNT_BIG(*) AS MismatchCount
    FROM ClinicDB.dbo.Patients AS src
    WHERE NOT EXISTS (SELECT 1 FROM ClinicMedicalRecordsDB.dbo.Patients AS tgt WHERE tgt.Id = src.Id)
    UNION ALL SELECT N'Source MedicalReports missing from target', COUNT_BIG(*)
    FROM ClinicDB.dbo.MedicalReports AS src
    WHERE NOT EXISTS (SELECT 1 FROM ClinicMedicalRecordsDB.dbo.MedicalReports AS tgt WHERE tgt.Id = src.Id)
    UNION ALL SELECT N'Patient identity link mismatch', COUNT_BIG(*)
    FROM ClinicDB.dbo.Patients AS src
    INNER JOIN ClinicMedicalRecordsDB.dbo.Patients AS tgt ON tgt.Id = src.Id
    WHERE ISNULL(CONVERT(nvarchar(36), tgt.PersonId), N'') <> CONVERT(nvarchar(36), src.PersonId)
       OR ISNULL(CONVERT(nvarchar(36), tgt.IdentityUserId), N'') <> ISNULL(CONVERT(nvarchar(36), (SELECT p.IdentityUserId FROM ClinicDB.dbo.Persons p WHERE p.Id = src.PersonId)), N'')
    UNION ALL SELECT N'Patients missing FullName', COUNT_BIG(*)
    FROM ClinicMedicalRecordsDB.dbo.Patients AS p
    WHERE p.FullName IS NULL OR p.FullName = ''

    UNION ALL SELECT N'MedicalReports missing Patient reference', COUNT_BIG(*)
    FROM ClinicMedicalRecordsDB.dbo.MedicalReports AS mr
    WHERE NOT EXISTS (SELECT 1 FROM ClinicMedicalRecordsDB.dbo.Patients AS p WHERE p.Id = mr.PatientId)

    UNION ALL SELECT N'MedicalReports with duplicate AppointmentId', COUNT_BIG(*)
    FROM (
        SELECT AppointmentId
        FROM ClinicMedicalRecordsDB.dbo.MedicalReports
        GROUP BY AppointmentId
        HAVING COUNT(*) > 1
    ) AS dupApps

    UNION ALL SELECT N'MedicalReports missing DoctorId', COUNT_BIG(*)
    FROM ClinicMedicalRecordsDB.dbo.MedicalReports AS mr
    WHERE mr.DoctorId IS NULL OR mr.DoctorId = '00000000-0000-0000-0000-000000000000'

    UNION ALL SELECT N'MedicalReports with invalid Status', COUNT_BIG(*)
    FROM ClinicMedicalRecordsDB.dbo.MedicalReports AS mr
    WHERE mr.Status NOT IN (N'Draft', N'Finalized')

) AS integrityChecks;

SELECT CheckName, MismatchCount FROM @Checks ORDER BY CheckName;
IF EXISTS (SELECT 1 FROM @Checks WHERE MismatchCount <> 0)
    THROW 51002, 'Medical Records import verification failed. Review mismatch counts above.', 1;
