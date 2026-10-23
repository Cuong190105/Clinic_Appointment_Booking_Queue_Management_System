/*
  Verification script for ClinicQueueDB data import.
  Ensures row count parity, data integrity, and constraint adherence.
*/
SET NOCOUNT ON;

DECLARE @SourceTickets INT = (SELECT COUNT(*) FROM ClinicDB.dbo.QueueTickets);
DECLARE @TargetTickets INT = (SELECT COUNT(*) FROM ClinicQueueDB.dbo.QueueTickets);
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

SELECT 
    @SourceTickets AS SourceTicketsCount,
    @TargetTickets AS TargetTicketsCount,
    @TargetCounters AS TargetCountersCount,
    @DuplicateAppointments AS DuplicateAppointmentsCount,
    @DuplicateNumbers AS DuplicateNumbersCount,
    @MissingNames AS MissingNamesCount;

IF (@SourceTickets <> @TargetTickets)
    THROW 52000, 'Ticket count mismatch between ClinicDB and ClinicQueueDB.', 1;

IF (@DuplicateAppointments > 0)
    THROW 52001, 'Duplicate AppointmentId found in ClinicQueueDB.QueueTickets.', 1;

IF (@DuplicateNumbers > 0)
    THROW 52002, 'Duplicate QueueNumber within (DoctorId, QueueDate) in ClinicQueueDB.', 1;

IF (@MissingNames > 0)
    THROW 52003, 'QueueTickets found with NULL PatientName or DoctorName.', 1;

PRINT 'All ClinicQueueDB verification checks passed successfully.';
