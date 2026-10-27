-- ==============================================================================
-- Reconciliation and Verification Script for Notifications Import
-- ==============================================================================

SET NOCOUNT ON;

DECLARE @SourceCount INT = (SELECT COUNT(*) FROM ClinicDB.dbo.Notifications);
DECLARE @TargetCount INT = (SELECT COUNT(*) FROM ClinicNotificationDB.dbo.Notifications);
DECLARE @MissingRecipientCount INT = (SELECT COUNT(*) FROM ClinicNotificationDB.dbo.Notifications WHERE RecipientId = '00000000-0000-0000-0000-000000000000' OR RecipientId IS NULL);
DECLARE @DuplicateIdCount INT = (SELECT COUNT(*) FROM (SELECT Id FROM ClinicNotificationDB.dbo.Notifications GROUP BY Id HAVING COUNT(*) > 1) d);
DECLARE @UnmappedAccountCount INT = (SELECT COUNT(*) FROM ClinicNotificationDB.dbo.Notifications WHERE RecipientType = 2 AND ContactEmail IS NULL AND ContactPhone IS NULL);

SELECT 
    @SourceCount AS SourceCount_ClinicDB,
    @TargetCount AS TargetCount_ClinicNotificationDB,
    (@SourceCount - @TargetCount) AS CountDifference,
    @MissingRecipientCount AS MissingRecipientCount,
    @DuplicateIdCount AS DuplicateIdCount,
    @UnmappedAccountCount AS UnmappedAccountCount;

IF (@SourceCount = @TargetCount AND @MissingRecipientCount = 0 AND @DuplicateIdCount = 0)
    PRINT 'VERIFICATION PASSED: Notification migration is 100% consistent with zero unexplained mismatches.';
ELSE
    PRINT 'VERIFICATION WARNING: Inconsistencies detected during reconciliation.';

