/*
  Additive, re-runnable Queue data import.
  Copies data from ClinicDB (and appointment/patient/doctor graphs) to ClinicQueueDB.
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

    IF DB_ID(N'ClinicQueueDB') IS NULL
        THROW 51001, 'Target ClinicQueueDB does not exist on this SQL Server.', 1;

    -- 1. Import QueueTickets with Denormalized Snapshots
    INSERT INTO ClinicQueueDB.dbo.QueueTickets (
        Id,
        AppointmentId,
        PatientId,
        DoctorId,
        QueueDate,
        QueueNumber,
        PatientName,
        DoctorName,
        Priority,
        Status,
        CheckInTime,
        CalledAt,
        ExamStartedAt,
        CompletedAt,
        SkippedAt,
        CreatedAt,
        UpdatedAt,
        IsDeleted
    )
    SELECT q.Id,
           q.AppointmentId,
           a.PatientId,
           a.DoctorId,
           CAST((q.CheckInTime AT TIME ZONE 'UTC' AT TIME ZONE '$(ClinicWindowsTimeZone)') AS date) AS QueueDate,
           q.QueueNumber,
           COALESCE(pPer.FullName, N'Unknown Patient') AS PatientName,
           COALESCE(a.DoctorName, dPer.FullName, N'Unknown Doctor') AS DoctorName,
           q.Priority,
           q.Status,
           q.CheckInTime,
           q.CalledAt,
           CASE WHEN q.Status IN ('InProgress', 'Completed') THEN q.CalledAt ELSE NULL END AS ExamStartedAt,
           CASE WHEN q.Status = 'Completed' THEN COALESCE(q.UpdatedAt, q.CalledAt, q.CheckInTime) ELSE NULL END AS CompletedAt,
           CASE WHEN q.Status = 'Skipped' THEN COALESCE(q.UpdatedAt, q.CalledAt, q.CheckInTime) ELSE NULL END AS SkippedAt,
           q.CreatedAt,
           q.UpdatedAt,
           q.IsDeleted
    FROM ClinicDB.dbo.QueueTickets AS q
    JOIN ClinicDB.dbo.Appointments AS a ON q.AppointmentId = a.Id
    LEFT JOIN ClinicDB.dbo.Patients AS pat ON a.PatientId = pat.Id
    LEFT JOIN ClinicDB.dbo.Persons AS pPer ON pat.PersonId = pPer.Id
    LEFT JOIN ClinicDB.dbo.Doctors AS doc ON a.DoctorId = doc.Id
    LEFT JOIN ClinicDB.dbo.Employees AS emp ON doc.EmployeeId = emp.Id
    LEFT JOIN ClinicDB.dbo.Persons AS dPer ON emp.PersonId = dPer.Id
    WHERE NOT EXISTS (
        SELECT 1 FROM ClinicQueueDB.dbo.QueueTickets AS target
        WHERE target.Id = q.Id OR target.AppointmentId = q.AppointmentId
    );

    -- 2. Import QueueCounters (Derived from Tickets & Source QueueCounters)
    WITH AggregatedCounters AS (
        SELECT DoctorId,
               QueueDate,
               MAX(QueueNumber) AS MaxNumber,
               MAX(CheckInTime) AS MaxTime
        FROM ClinicQueueDB.dbo.QueueTickets
        GROUP BY DoctorId, QueueDate
        UNION ALL
        SELECT qc.DoctorId,
               qc.Date AS QueueDate,
               qc.CurrentNumber AS MaxNumber,
               GETUTCDATE() AS MaxTime
        FROM ClinicDB.dbo.QueueCounters AS qc
    ),
    UnifiedCounters AS (
        SELECT DoctorId,
               QueueDate,
               MAX(MaxNumber) AS LastNumber,
               MAX(MaxTime) AS UpdatedAt
        FROM AggregatedCounters
        GROUP BY DoctorId, QueueDate
    )
    MERGE INTO ClinicQueueDB.dbo.QueueCounters AS target
    USING UnifiedCounters AS src
    ON target.DoctorId = src.DoctorId AND target.QueueDate = src.QueueDate
    WHEN MATCHED AND src.LastNumber > target.LastNumber THEN
        UPDATE SET target.LastNumber = src.LastNumber, target.UpdatedAt = src.UpdatedAt
    WHEN NOT MATCHED THEN
        INSERT (DoctorId, QueueDate, LastNumber, UpdatedAt)
        VALUES (src.DoctorId, src.QueueDate, src.LastNumber, src.UpdatedAt);

    COMMIT TRANSACTION;
    PRINT 'ClinicQueueDB data import completed successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    DECLARE @errMsg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR('Queue import failed: %s', 16, 1, @errMsg);
END CATCH;
