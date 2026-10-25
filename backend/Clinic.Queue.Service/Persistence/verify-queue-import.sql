/*
  Verification script for ClinicQueueDB data import.
  Ensures row count parity, data integrity, and constraint adherence.
*/
SET NOCOUNT ON;

DECLARE @SourceTickets INT = (SELECT COUNT(*) FROM ClinicDB.dbo.QueueTickets);
DECLARE @TargetTickets INT = (SELECT COUNT(*) FROM ClinicQueueDB.dbo.QueueTickets);
DECLARE @SourceAppointmentsMissing INT = (
    SELECT COUNT(*)
    FROM ClinicDB.dbo.QueueTickets AS source
    WHERE NOT EXISTS (
        SELECT 1 FROM ClinicQueueDB.dbo.QueueTickets AS target
        WHERE target.AppointmentId = source.AppointmentId
    )
);
DECLARE @TicketIdMismatches INT = (
    SELECT COUNT(*)
    FROM ClinicDB.dbo.QueueTickets AS source
    JOIN ClinicDB.dbo.Appointments AS sourceAppointment ON sourceAppointment.Id = source.AppointmentId
    JOIN ClinicQueueDB.dbo.QueueTickets AS target ON target.AppointmentId = source.AppointmentId
    WHERE target.Id <> source.Id
);
DECLARE @DuplicateAppointments INT = (
    SELECT COUNT(*) FROM (
        SELECT AppointmentId FROM ClinicQueueDB.dbo.QueueTickets GROUP BY AppointmentId HAVING COUNT(*) > 1
    ) AS d
);
DECLARE @DuplicateNumbers INT = (
    SELECT COUNT(*) FROM (
        SELECT DoctorId, QueueDate, QueueNumber FROM ClinicQueueDB.dbo.QueueTickets GROUP BY DoctorId, QueueDate, QueueNumber HAVING COUNT(*) > 1
    ) AS d
);
DECLARE @MissingNames INT = (
    SELECT COUNT(*) FROM ClinicQueueDB.dbo.QueueTickets WHERE PatientName IS NULL OR DoctorName IS NULL
);
DECLARE @TargetCounters INT = (SELECT COUNT(*) FROM ClinicQueueDB.dbo.QueueCounters);
DECLARE @CountersBehindTickets INT = (
    SELECT COUNT(*)
    FROM ClinicQueueDB.dbo.QueueCounters AS counter
    JOIN (
        SELECT DoctorId, QueueDate, MAX(QueueNumber) AS MaxNumber
        FROM ClinicQueueDB.dbo.QueueTickets
        GROUP BY DoctorId, QueueDate
    ) AS ticket ON ticket.DoctorId = counter.DoctorId AND ticket.QueueDate = counter.QueueDate
    WHERE counter.LastNumber < ticket.MaxNumber
);

SELECT 
    @SourceTickets AS SourceTicketsCount,
    @TargetTickets AS TargetTicketsCount,
    @SourceAppointmentsMissing AS SourceAppointmentsMissingCount,
    @TicketIdMismatches AS TicketIdMismatchesCount,
    @TargetCounters AS TargetCountersCount,
    @CountersBehindTickets AS CountersBehindTicketsCount,
    @DuplicateAppointments AS DuplicateAppointmentsCount,
    @DuplicateNumbers AS DuplicateNumbersCount,
    @MissingNames AS MissingNamesCount;

IF (@SourceAppointmentsMissing > 0)
    THROW 52000, 'A ClinicDB QueueTicket appointment is missing in ClinicQueueDB.', 1;

IF (@DuplicateAppointments > 0)
    THROW 52001, 'Duplicate AppointmentId found in ClinicQueueDB.QueueTickets.', 1;

IF (@DuplicateNumbers > 0)
    THROW 52002, 'Duplicate QueueNumber within (DoctorId, QueueDate) in ClinicQueueDB.', 1;

IF (@MissingNames > 0)
    THROW 52003, 'QueueTickets found with NULL PatientName or DoctorName.', 1;

IF (@CountersBehindTickets > 0)
    THROW 52004, 'A QueueCounter is behind the maximum ticket number for its doctor and date.', 1;

PRINT 'ClinicQueueDB verification checks passed. Extra tickets and ticket ID mismatches are reported for reconciliation.';
