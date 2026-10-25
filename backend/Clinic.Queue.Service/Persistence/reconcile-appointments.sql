/*
  Read-only operational reconciliation for the shared SQL Server development setup.
  Reports state mismatches between ClinicAppointmentDB and ClinicQueueDB by AppointmentId.
  It intentionally does not change data or allocate queue numbers.
*/
SET NOCOUNT ON;

IF DB_ID(N'ClinicAppointmentDB') IS NULL
    THROW 52100, 'ClinicAppointmentDB does not exist on this SQL Server.', 1;
IF DB_ID(N'ClinicQueueDB') IS NULL
    THROW 52101, 'ClinicQueueDB does not exist on this SQL Server.', 1;

CREATE TABLE #Mismatches (
    MismatchType nvarchar(64) NOT NULL,
    AppointmentId uniqueidentifier NOT NULL,
    QueueTicketId uniqueidentifier NULL,
    AppointmentStatus nvarchar(32) NULL,
    QueueStatus nvarchar(32) NULL
);

;WITH Mismatches AS (
    SELECT
        N'CheckedInWithoutTicket' AS MismatchType,
        appointment.Id AS AppointmentId,
        CAST(NULL AS uniqueidentifier) AS QueueTicketId,
        CAST(appointment.Status AS nvarchar(32)) AS AppointmentStatus,
        CAST(NULL AS nvarchar(32)) AS QueueStatus
    FROM ClinicAppointmentDB.dbo.Appointments AS appointment
    WHERE appointment.IsDeleted = 0
      AND appointment.Status = N'CheckedIn'
      AND NOT EXISTS (
          SELECT 1 FROM ClinicQueueDB.dbo.QueueTickets AS ticket
          WHERE ticket.AppointmentId = appointment.Id AND ticket.IsDeleted = 0
      )

    UNION ALL

    SELECT
        N'CompletedAppointmentWithoutCompletedTicket',
        appointment.Id,
        ticket.Id,
        CAST(appointment.Status AS nvarchar(32)),
        CAST(ticket.Status AS nvarchar(32))
    FROM ClinicAppointmentDB.dbo.Appointments AS appointment
    LEFT JOIN ClinicQueueDB.dbo.QueueTickets AS ticket
        ON ticket.AppointmentId = appointment.Id AND ticket.IsDeleted = 0
    WHERE appointment.IsDeleted = 0
      AND appointment.Status = N'Completed'
      AND (ticket.Id IS NULL OR ticket.Status <> N'Completed')

    UNION ALL

    SELECT
        N'CancelledAppointmentWithActiveTicket',
        appointment.Id,
        ticket.Id,
        CAST(appointment.Status AS nvarchar(32)),
        CAST(ticket.Status AS nvarchar(32))
    FROM ClinicAppointmentDB.dbo.Appointments AS appointment
    INNER JOIN ClinicQueueDB.dbo.QueueTickets AS ticket
        ON ticket.AppointmentId = appointment.Id AND ticket.IsDeleted = 0
    WHERE appointment.IsDeleted = 0
      AND appointment.Status = N'Cancelled'
      AND ticket.Status IN (N'Waiting', N'Called')

    UNION ALL

    SELECT
        N'QueueTicketWithoutAppointment',
        ticket.AppointmentId,
        ticket.Id,
        CAST(NULL AS nvarchar(32)),
        CAST(ticket.Status AS nvarchar(32))
    FROM ClinicQueueDB.dbo.QueueTickets AS ticket
    WHERE ticket.IsDeleted = 0
      AND NOT EXISTS (
          SELECT 1 FROM ClinicAppointmentDB.dbo.Appointments AS appointment
          WHERE appointment.Id = ticket.AppointmentId AND appointment.IsDeleted = 0
      )
)
INSERT INTO #Mismatches (MismatchType, AppointmentId, QueueTicketId, AppointmentStatus, QueueStatus)
SELECT MismatchType, AppointmentId, QueueTicketId, AppointmentStatus, QueueStatus FROM Mismatches;

SELECT MismatchType, COUNT_BIG(*) AS MismatchCount
FROM #Mismatches
GROUP BY MismatchType
ORDER BY MismatchType;

SELECT MismatchType, AppointmentId, QueueTicketId, AppointmentStatus, QueueStatus
FROM #Mismatches
ORDER BY MismatchType, AppointmentId;
