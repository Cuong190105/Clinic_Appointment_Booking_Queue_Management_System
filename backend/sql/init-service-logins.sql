-- Idempotent initialization for shared SQL Server instance:
-- Creates the 6 logical databases, configures RCSI & SIMPLE recovery,
-- and provisions 6 least-privilege service logins (1 db_owner per database).

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
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'ClinicSchedulingDB') <> N'SIMPLE'
    ALTER DATABASE [ClinicSchedulingDB] SET RECOVERY SIMPLE;
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
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'ClinicAppointmentDB') <> N'SIMPLE'
    ALTER DATABASE [ClinicAppointmentDB] SET RECOVERY SIMPLE;
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
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'ClinicQueueDB') <> N'SIMPLE'
    ALTER DATABASE [ClinicQueueDB] SET RECOVERY SIMPLE;
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
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'ClinicMedicalRecordsDB') <> N'SIMPLE'
    ALTER DATABASE [ClinicMedicalRecordsDB] SET RECOVERY SIMPLE;
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
ALTER ROLE [db_owner] ADD MEMBER [svc_notification];
GO
