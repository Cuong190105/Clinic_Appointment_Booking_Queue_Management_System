/*
  Post-import Medical Records reconciliation report.
  Compares row counts and validates relational integrity between ClinicDB and ClinicMedicalRecordsDB.
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
SELECT CheckName, MismatchCount
FROM
(
    SELECT N'Patients missing FullName' AS CheckName, COUNT_BIG(*) AS MismatchCount
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

    UNION ALL SELECT N'Status mismatch between ClinicDB and MedicalRecordsDB', COUNT_BIG(*)
    FROM ClinicDB.dbo.MedicalReports AS src
    INNER JOIN ClinicMedicalRecordsDB.dbo.MedicalReports AS tgt ON src.Id = tgt.Id
    WHERE (tgt.Status = N'Finalized' AND src.Status NOT IN (N'Finalized', N'1'))
       OR (tgt.Status = N'Draft' AND src.Status IN (N'Finalized', N'1'))
) AS integrityChecks;
