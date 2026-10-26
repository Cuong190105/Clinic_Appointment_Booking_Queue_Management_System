-- ==============================================================================
-- Idempotent Data Import: ClinicDB.dbo.Notifications -> ClinicNotificationDB
-- ==============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- Pre-import validation
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ClinicNotificationDB')
BEGIN
    RAISERROR('Target database ClinicNotificationDB does not exist.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

-- Import Notifications with RecipientId resolved from Identity Users
INSERT INTO ClinicNotificationDB.dbo.Notifications
(
    Id,
    RecipientId,
    RecipientType,
    NotificationType,
    Title,
    Body,
    IsRead,
    ReadAt,
    ContactName,
    ContactEmail,
    ContactPhone,
    AppointmentId,
    QueueTicketId,
    CorrelationId,
    CreatedAt,
    ScheduledFor
)
SELECT
    n.Id,
    ISNULL(u.Id, n.PersonId) AS RecipientId,
    CASE WHEN u.Id IS NOT NULL THEN 1 ELSE 2 END AS RecipientType, -- 1 = Account, 2 = Guest
    CASE 
        WHEN n.Type = 'AppointmentConfirmation' THEN 1
        WHEN n.Type = 'AppointmentCancellation' THEN 2
        WHEN n.Type = 'AppointmentReminder' THEN 3
        WHEN n.Type = 'PatientCalled' THEN 4
        WHEN n.Type = 'PasswordReset' THEN 5
        ELSE 1
    END AS NotificationType,
    ISNULL(n.Title, 'Thông báo') AS Title,
    ISNULL(n.Message, '') AS Body,
    ISNULL(n.IsRead, 0) AS IsRead,
    NULL AS ReadAt,
    p.FullName AS ContactName,
    p.Email AS ContactEmail,
    p.PhoneNumber AS ContactPhone,
    NULL AS AppointmentId,
    NULL AS QueueTicketId,
    NULL AS CorrelationId,
    n.CreatedAt,
    n.ScheduledAt AS ScheduledFor
FROM ClinicDB.dbo.Notifications n
LEFT JOIN ClinicIdentityDB.dbo.Users u ON n.PersonId = u.PersonId
LEFT JOIN ClinicIdentityDB.dbo.Persons p ON n.PersonId = p.Id
WHERE NOT EXISTS (
    SELECT 1 FROM ClinicNotificationDB.dbo.Notifications target
    WHERE target.Id = n.Id
);

COMMIT TRANSACTION;
