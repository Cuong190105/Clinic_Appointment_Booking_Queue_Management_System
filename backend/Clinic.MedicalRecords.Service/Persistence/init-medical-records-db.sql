SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE TABLE [EncounterContexts] (
        [AppointmentId] uniqueidentifier NOT NULL,
        [QueueTicketId] uniqueidentifier NOT NULL,
        [PatientId] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [DoctorName] nvarchar(200) NULL,
        [QueueDate] date NULL,
        [Status] nvarchar(20) NOT NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [CompletedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_EncounterContexts] PRIMARY KEY ([AppointmentId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE TABLE [InboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [ConsumerName] nvarchar(100) NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [EventType] nvarchar(100) NOT NULL,
        [SchemaVersion] int NOT NULL,
        [CorrelationId] uniqueidentifier NULL,
        [Status] nvarchar(20) NOT NULL,
        [ReceivedAt] datetimeoffset NOT NULL,
        [ProcessedAt] datetimeoffset NULL,
        [Error] nvarchar(max) NULL,
        CONSTRAINT [PK_InboxMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE TABLE [MedicalAccessAudits] (
        [Id] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [ActorRole] nvarchar(50) NOT NULL,
        [ActorDoctorId] uniqueidentifier NULL,
        [ActorPatientId] uniqueidentifier NULL,
        [AppointmentId] uniqueidentifier NULL,
        [PatientId] uniqueidentifier NULL,
        [MedicalReportId] uniqueidentifier NULL,
        [Action] nvarchar(50) NOT NULL,
        [Outcome] nvarchar(20) NOT NULL,
        [IpAddress] nvarchar(50) NULL,
        [Timestamp] datetimeoffset NOT NULL,
        [CorrelationId] uniqueidentifier NULL,
        CONSTRAINT [PK_MedicalAccessAudits] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE TABLE [Patients] (
        [Id] uniqueidentifier NOT NULL,
        [IdentityUserId] uniqueidentifier NULL,
        [FullName] nvarchar(200) NOT NULL,
        [PhoneNumber] nvarchar(20) NULL,
        [Email] nvarchar(200) NULL,
        [DateOfBirth] date NOT NULL,
        [Gender] nvarchar(20) NOT NULL,
        [Address] nvarchar(500) NULL,
        [InsuranceNumber] nvarchar(50) NULL,
        [EmergencyContact] nvarchar(50) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Patients] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE TABLE [MedicalReports] (
        [Id] uniqueidentifier NOT NULL,
        [AppointmentId] uniqueidentifier NOT NULL,
        [PatientId] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [DoctorName] nvarchar(200) NULL,
        [SpecialtyName] nvarchar(200) NULL,
        [ScheduleDate] date NULL,
        [ExamStartTime] datetimeoffset NULL,
        [ExamEndTime] datetimeoffset NULL,
        [Symptoms] nvarchar(2000) NULL,
        [Diagnosis] nvarchar(2000) NULL,
        [Prescription] nvarchar(4000) NULL,
        [Notes] nvarchar(2000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_MedicalReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MedicalReports_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE TABLE [MedicalReportAmendments] (
        [Id] uniqueidentifier NOT NULL,
        [MedicalReportId] uniqueidentifier NOT NULL,
        [AppointmentId] uniqueidentifier NOT NULL,
        [AmendmentNumber] int NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [Symptoms] nvarchar(2000) NULL,
        [Diagnosis] nvarchar(2000) NULL,
        [Prescription] nvarchar(4000) NULL,
        [Notes] nvarchar(2000) NULL,
        [AuthorDoctorId] uniqueidentifier NOT NULL,
        [AuthorName] nvarchar(200) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_MedicalReportAmendments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MedicalReportAmendments_MedicalReports_MedicalReportId] FOREIGN KEY ([MedicalReportId]) REFERENCES [MedicalReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_EncounterContexts_DoctorId] ON [EncounterContexts] ([DoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_EncounterContexts_PatientId] ON [EncounterContexts] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_EncounterContexts_QueueDate] ON [EncounterContexts] ([QueueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InboxMessages_ConsumerName_EventId] ON [InboxMessages] ([ConsumerName], [EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_InboxMessages_ReceivedAt] ON [InboxMessages] ([ReceivedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_MedicalAccessAudits_ActorUserId] ON [MedicalAccessAudits] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_MedicalAccessAudits_AppointmentId] ON [MedicalAccessAudits] ([AppointmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_MedicalAccessAudits_MedicalReportId] ON [MedicalAccessAudits] ([MedicalReportId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_MedicalAccessAudits_PatientId] ON [MedicalAccessAudits] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_MedicalAccessAudits_Timestamp] ON [MedicalAccessAudits] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_MedicalReportAmendments_AppointmentId] ON [MedicalReportAmendments] ([AppointmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_MedicalReportAmendments_MedicalReportId] ON [MedicalReportAmendments] ([MedicalReportId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MedicalReportAmendments_MedicalReportId_AmendmentNumber] ON [MedicalReportAmendments] ([MedicalReportId], [AmendmentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MedicalReports_AppointmentId] ON [MedicalReports] ([AppointmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_MedicalReports_DoctorId] ON [MedicalReports] ([DoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_MedicalReports_PatientId] ON [MedicalReports] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_MedicalReports_ScheduleDate] ON [MedicalReports] ([ScheduleDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_MedicalReports_Status] ON [MedicalReports] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_Patients_FullName] ON [Patients] ([FullName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Patients_IdentityUserId] ON [Patients] ([IdentityUserId]) WHERE [IdentityUserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    CREATE INDEX [IX_Patients_PhoneNumber] ON [Patients] ([PhoneNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026101939_InitialMedicalRecordsSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261026101939_InitialMedicalRecordsSchema', N'10.0.9');
END;

COMMIT;
GO

