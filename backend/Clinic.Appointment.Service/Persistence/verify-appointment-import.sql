/*
  Post-import Appointment reconciliation report.
  Compares row counts and validates relational integrity between ClinicDB and ClinicAppointmentDB.
  All MismatchCount values should be 0.
*/
SET NOCOUNT ON;

IF DB_ID(N'ClinicDB') IS NULL OR DB_ID(N'ClinicAppointmentDB') IS NULL
    THROW 51001, 'ClinicDB and ClinicAppointmentDB must both exist on this SQL Server.', 1;

-- 1. Row count comparison
SELECT Entity, ClinicDbRows, AppointmentDbRows
FROM
(
    SELECT N'Appointments' AS Entity,
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.Appointments WHERE IsDeleted = 0) AS ClinicDbRows,
           (SELECT COUNT_BIG(*) FROM ClinicAppointmentDB.dbo.Appointments WHERE IsDeleted = 0) AS AppointmentDbRows
    UNION ALL SELECT N'AppointmentSnapshots',
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.AppointmentSnapshots),
           (SELECT COUNT_BIG(*) FROM ClinicAppointmentDB.dbo.AppointmentSnapshots)
    UNION ALL SELECT N'PatientProjections',
           (SELECT COUNT_BIG(*) FROM ClinicDB.dbo.Patients WHERE IsDeleted = 0),
           (SELECT COUNT_BIG(*) FROM ClinicAppointmentDB.dbo.PatientProjections)
) AS counts
ORDER BY Entity;

-- 2. Data integrity and denormalized checks
SELECT CheckName, MismatchCount
FROM
(
    SELECT N'Appointments missing PatientName' AS CheckName, COUNT_BIG(*) AS MismatchCount
    FROM ClinicAppointmentDB.dbo.Appointments AS a
    WHERE a.PatientName IS NULL OR a.PatientName = ''

    UNION ALL SELECT N'Appointments missing DoctorName', COUNT_BIG(*)
    FROM ClinicAppointmentDB.dbo.Appointments AS a
    WHERE a.DoctorName IS NULL OR a.DoctorName = ''

    UNION ALL SELECT N'Appointments with missing or empty ScheduleDate', COUNT_BIG(*)
    FROM ClinicAppointmentDB.dbo.Appointments AS a
    WHERE a.ScheduleDate IS NULL

    UNION ALL SELECT N'Snapshots with missing Appointment link', COUNT_BIG(*)
    FROM ClinicAppointmentDB.dbo.AppointmentSnapshots AS s
    WHERE NOT EXISTS (SELECT 1 FROM ClinicAppointmentDB.dbo.Appointments AS a WHERE a.Id = s.AppointmentId)
) AS integrityChecks;

