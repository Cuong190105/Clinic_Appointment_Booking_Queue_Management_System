-- Idempotent initialization for shared SQL Server instance:
-- Creates the 6 logical databases, configures RCSI and the recovery model required by the backup
-- policy (docs/BACKUP_RECOVERY.md), provisions 6 least-privilege service logins (1 db_owner per
-- database) and the svc_backup login used by the db-backup sidecar.
--
-- Recovery models: FULL where the RPO needs point-in-time restore from transaction-log backups
-- (Medical Records, Appointment, Scheduling, Queue); SIMPLE where full + differential backups meet
-- the RPO (Identity, Notification). FULL requires regular log backups, otherwise the log grows.
--
-- Users are re-mapped to their login on every run (ALTER USER ... WITH LOGIN) so databases restored
-- onto a rebuilt server, where logins get new SIDs, are not left with orphaned users.

-- 1. ClinicIdentityDB -> svc_identity
IF DB_ID(N'ClinicIdentityDB') IS NULL
    CREATE DATABASE [ClinicIdentityDB];
GO
IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'ClinicIdentityDB') = 0
    ALTER DATABASE [ClinicIdentityDB] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'ClinicIdentityDB') <> N'SIMPLE'
    ALTER DATABASE [ClinicIdentityDB] SET RECOVERY SIMPLE;
GO
IF SUSER_ID(N'svc_identity') IS NULL
    CREATE LOGIN [svc_identity] WITH PASSWORD = N'$(IdentityPassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [svc_identity] WITH PASSWORD = N'$(IdentityPassword)';
GO
USE [ClinicIdentityDB];
GO
IF USER_ID(N'svc_identity') IS NULL
    CREATE USER [svc_identity] FOR LOGIN [svc_identity];
ELSE
    ALTER USER [svc_identity] WITH LOGIN = [svc_identity];
ALTER ROLE [db_owner] ADD MEMBER [svc_identity];
GO

-- 2. ClinicSchedulingDB -> svc_scheduling
USE [master];
GO
IF DB_ID(N'ClinicSchedulingDB') IS NULL
    CREATE DATABASE [ClinicSchedulingDB];
GO
IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'ClinicSchedulingDB') = 0
    ALTER DATABASE [ClinicSchedulingDB] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'ClinicSchedulingDB') <> N'FULL'
    ALTER DATABASE [ClinicSchedulingDB] SET RECOVERY FULL;
GO
IF SUSER_ID(N'svc_scheduling') IS NULL
    CREATE LOGIN [svc_scheduling] WITH PASSWORD = N'$(SchedulingPassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [svc_scheduling] WITH PASSWORD = N'$(SchedulingPassword)';
GO
USE [ClinicSchedulingDB];
GO
IF USER_ID(N'svc_scheduling') IS NULL
    CREATE USER [svc_scheduling] FOR LOGIN [svc_scheduling];
ELSE
    ALTER USER [svc_scheduling] WITH LOGIN = [svc_scheduling];
ALTER ROLE [db_owner] ADD MEMBER [svc_scheduling];
GO

-- 3. ClinicAppointmentDB -> svc_appointment
USE [master];
GO
IF DB_ID(N'ClinicAppointmentDB') IS NULL
    CREATE DATABASE [ClinicAppointmentDB];
GO
IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'ClinicAppointmentDB') = 0
    ALTER DATABASE [ClinicAppointmentDB] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'ClinicAppointmentDB') <> N'FULL'
    ALTER DATABASE [ClinicAppointmentDB] SET RECOVERY FULL;
GO
IF SUSER_ID(N'svc_appointment') IS NULL
    CREATE LOGIN [svc_appointment] WITH PASSWORD = N'$(AppointmentPassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [svc_appointment] WITH PASSWORD = N'$(AppointmentPassword)';
GO
USE [ClinicAppointmentDB];
GO
IF USER_ID(N'svc_appointment') IS NULL
    CREATE USER [svc_appointment] FOR LOGIN [svc_appointment];
ELSE
    ALTER USER [svc_appointment] WITH LOGIN = [svc_appointment];
ALTER ROLE [db_owner] ADD MEMBER [svc_appointment];
GO

-- 4. ClinicQueueDB -> svc_queue
USE [master];
GO
IF DB_ID(N'ClinicQueueDB') IS NULL
    CREATE DATABASE [ClinicQueueDB];
GO
IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'ClinicQueueDB') = 0
    ALTER DATABASE [ClinicQueueDB] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'ClinicQueueDB') <> N'FULL'
    ALTER DATABASE [ClinicQueueDB] SET RECOVERY FULL;
GO
IF SUSER_ID(N'svc_queue') IS NULL
    CREATE LOGIN [svc_queue] WITH PASSWORD = N'$(QueuePassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [svc_queue] WITH PASSWORD = N'$(QueuePassword)';
GO
USE [ClinicQueueDB];
GO
IF USER_ID(N'svc_queue') IS NULL
    CREATE USER [svc_queue] FOR LOGIN [svc_queue];
ELSE
    ALTER USER [svc_queue] WITH LOGIN = [svc_queue];
ALTER ROLE [db_owner] ADD MEMBER [svc_queue];
GO

-- 5. ClinicMedicalRecordsDB -> svc_medical_records
USE [master];
GO
IF DB_ID(N'ClinicMedicalRecordsDB') IS NULL
    CREATE DATABASE [ClinicMedicalRecordsDB];
GO
IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'ClinicMedicalRecordsDB') = 0
    ALTER DATABASE [ClinicMedicalRecordsDB] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'ClinicMedicalRecordsDB') <> N'FULL'
    ALTER DATABASE [ClinicMedicalRecordsDB] SET RECOVERY FULL;
GO
IF SUSER_ID(N'svc_medical_records') IS NULL
    CREATE LOGIN [svc_medical_records] WITH PASSWORD = N'$(MedicalRecordsPassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [svc_medical_records] WITH PASSWORD = N'$(MedicalRecordsPassword)';
GO
USE [ClinicMedicalRecordsDB];
GO
IF USER_ID(N'svc_medical_records') IS NULL
    CREATE USER [svc_medical_records] FOR LOGIN [svc_medical_records];
ELSE
    ALTER USER [svc_medical_records] WITH LOGIN = [svc_medical_records];
ALTER ROLE [db_owner] ADD MEMBER [svc_medical_records];
GO

-- 6. ClinicNotificationDB -> svc_notification
USE [master];
GO
IF DB_ID(N'ClinicNotificationDB') IS NULL
    CREATE DATABASE [ClinicNotificationDB];
GO
IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'ClinicNotificationDB') = 0
    ALTER DATABASE [ClinicNotificationDB] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'ClinicNotificationDB') <> N'SIMPLE'
    ALTER DATABASE [ClinicNotificationDB] SET RECOVERY SIMPLE;
GO
IF SUSER_ID(N'svc_notification') IS NULL
    CREATE LOGIN [svc_notification] WITH PASSWORD = N'$(NotificationPassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [svc_notification] WITH PASSWORD = N'$(NotificationPassword)';
GO
USE [ClinicNotificationDB];
GO
IF USER_ID(N'svc_notification') IS NULL
    CREATE USER [svc_notification] FOR LOGIN [svc_notification];
ELSE
    ALTER USER [svc_notification] WITH LOGIN = [svc_notification];
ALTER ROLE [db_owner] ADD MEMBER [svc_notification];
GO

-- 7. svc_backup -> db-backup sidecar (docs/BACKUP_RECOVERY.md)
-- dbcreator: restore drills into scratch databases and in-place disaster restores.
-- db_backupoperator in each database: BACKUP DATABASE / BACKUP LOG. VIEW SERVER STATE: log-chain status.
-- It has no data access (no db_datareader) and is not sysadmin.
USE [master];
GO
IF SUSER_ID(N'svc_backup') IS NULL
    CREATE LOGIN [svc_backup] WITH PASSWORD = N'$(BackupPassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [svc_backup] WITH PASSWORD = N'$(BackupPassword)';
GO
ALTER SERVER ROLE [dbcreator] ADD MEMBER [svc_backup];
GRANT VIEW SERVER STATE TO [svc_backup];
GO
DECLARE @db sysname, @sql nvarchar(max);
DECLARE backup_dbs CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.databases
    WHERE name IN (N'ClinicIdentityDB', N'ClinicSchedulingDB', N'ClinicAppointmentDB',
                   N'ClinicQueueDB', N'ClinicMedicalRecordsDB', N'ClinicNotificationDB');
OPEN backup_dbs;
FETCH NEXT FROM backup_dbs INTO @db;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'USE ' + QUOTENAME(@db) + N';
IF USER_ID(N''svc_backup'') IS NULL CREATE USER [svc_backup] FOR LOGIN [svc_backup];
ELSE ALTER USER [svc_backup] WITH LOGIN = [svc_backup];
ALTER ROLE [db_backupoperator] ADD MEMBER [svc_backup];';
    EXEC sys.sp_executesql @sql;
    FETCH NEXT FROM backup_dbs INTO @db;
END;
CLOSE backup_dbs;
DEALLOCATE backup_dbs;
GO
