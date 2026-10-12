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
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(300) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [Persons] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        [Email] nvarchar(200) NULL,
        [DateOfBirth] date NULL,
        [Gender] int NULL,
        [Address] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Persons] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [Description] nvarchar(300) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [Specialties] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Description] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Specialties] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [TemplateDataModel] (
        [Id] int NOT NULL IDENTITY,
        CONSTRAINT [PK_TemplateDataModel] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] uniqueidentifier NOT NULL,
        [PersonId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [HireDate] date NOT NULL,
        [ManagerId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Employees_Employees_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employees_Persons_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [Persons] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [PersonId] uniqueidentifier NOT NULL,
        [Type] nvarchar(30) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [Channel] nvarchar(20) NOT NULL,
        [SendTime] datetime2 NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Persons_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [Persons] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [Patients] (
        [Id] uniqueidentifier NOT NULL,
        [PersonId] uniqueidentifier NOT NULL,
        [InsuranceNumber] nvarchar(50) NULL,
        [EmergencyContact] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Patients] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Patients_Persons_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [Persons] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [PersonId] uniqueidentifier NOT NULL,
        [Username] nvarchar(100) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Persons_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [Persons] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [Id] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [Doctors] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [SpecialtyId] uniqueidentifier NOT NULL,
        [LicenseNumber] nvarchar(50) NOT NULL,
        [ExperienceYears] int NULL,
        [Qualification] nvarchar(300) NULL,
        [Biography] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Doctors] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Doctors_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Doctors_Specialties_SpecialtyId] FOREIGN KEY ([SpecialtyId]) REFERENCES [Specialties] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [WorkSchedules] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [ShiftStart] datetime2 NOT NULL,
        [ShiftEnd] datetime2 NOT NULL,
        [PatientLimit] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_WorkSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkSchedules_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [Appointments] (
        [Id] uniqueidentifier NOT NULL,
        [PatientId] uniqueidentifier NOT NULL,
        [WorkScheduleId] uniqueidentifier NOT NULL,
        [Reason] nvarchar(500) NULL,
        [TimeSlot] datetime2 NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [IsWalkIn] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Appointments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Appointments_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Appointments_WorkSchedules_WorkScheduleId] FOREIGN KEY ([WorkScheduleId]) REFERENCES [WorkSchedules] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [QueueTickets] (
        [Id] uniqueidentifier NOT NULL,
        [AppointmentId] uniqueidentifier NOT NULL,
        [QueueNumber] int NOT NULL,
        [Priority] bit NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CalledAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_QueueTickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QueueTickets_Appointments_AppointmentId] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE TABLE [MedicalReports] (
        [Id] uniqueidentifier NOT NULL,
        [QueueTicketId] uniqueidentifier NOT NULL,
        [Symptoms] nvarchar(1000) NULL,
        [Diagnosis] nvarchar(1000) NULL,
        [Prescription] nvarchar(2000) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_MedicalReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MedicalReports_QueueTickets_QueueTicketId] FOREIGN KEY ([QueueTicketId]) REFERENCES [QueueTickets] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Appointments_PatientId] ON [Appointments] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Appointments_WorkScheduleId] ON [Appointments] ([WorkScheduleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Doctors_EmployeeId] ON [Doctors] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Doctors_SpecialtyId] ON [Doctors] ([SpecialtyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Employees_ManagerId] ON [Employees] ([ManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Employees_PersonId] ON [Employees] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MedicalReports_QueueTicketId] ON [MedicalReports] ([QueueTicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_PersonId] ON [Notifications] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Patients_PersonId] ON [Patients] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_Name] ON [Permissions] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Persons_PhoneNumber] ON [Persons] ([PhoneNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QueueTickets_AppointmentId] ON [QueueTickets] ([AppointmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RolePermissions_RoleId_PermissionId] ON [RolePermissions] ([RoleId], [PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Specialties_Name] ON [Specialties] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserRoles_UserId_RoleId] ON [UserRoles] ([UserId], [RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_PersonId] ON [Users] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WorkSchedules_DoctorId] ON [WorkSchedules] ([DoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035815_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806035815_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806085622_AddModelUpdates'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(500) NOT NULL,
        [IssuedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806085622_AddModelUpdates'
)
BEGIN
    CREATE TABLE [ShiftRequests] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [ShiftStart] datetime2 NOT NULL,
        [ShiftEnd] datetime2 NOT NULL,
        [Reason] nvarchar(500) NULL,
        [Status] nvarchar(20) NOT NULL,
        [ApprovedWorkScheduleId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_ShiftRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShiftRequests_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShiftRequests_WorkSchedules_ApprovedWorkScheduleId] FOREIGN KEY ([ApprovedWorkScheduleId]) REFERENCES [WorkSchedules] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806085622_AddModelUpdates'
)
BEGIN
    CREATE TABLE [WorkHistories] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [SpecialtyId] uniqueidentifier NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_WorkHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkHistories_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkHistories_Specialties_SpecialtyId] FOREIGN KEY ([SpecialtyId]) REFERENCES [Specialties] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806085622_AddModelUpdates'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806085622_AddModelUpdates'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806085622_AddModelUpdates'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ShiftRequests_ApprovedWorkScheduleId] ON [ShiftRequests] ([ApprovedWorkScheduleId]) WHERE [ApprovedWorkScheduleId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806085622_AddModelUpdates'
)
BEGIN
    CREATE INDEX [IX_ShiftRequests_DoctorId] ON [ShiftRequests] ([DoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806085622_AddModelUpdates'
)
BEGIN
    CREATE INDEX [IX_WorkHistories_DoctorId] ON [WorkHistories] ([DoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806085622_AddModelUpdates'
)
BEGIN
    CREATE INDEX [IX_WorkHistories_SpecialtyId] ON [WorkHistories] ([SpecialtyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806085622_AddModelUpdates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806085622_AddModelUpdates', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    ALTER TABLE [Doctors] DROP CONSTRAINT [FK_Doctors_Specialties_SpecialtyId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    ALTER TABLE [ShiftRequests] DROP CONSTRAINT [FK_ShiftRequests_WorkSchedules_ApprovedWorkScheduleId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DROP TABLE [TemplateDataModel];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    ALTER TABLE [UserRoles] DROP CONSTRAINT [PK_UserRoles];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DROP INDEX [IX_UserRoles_UserId_RoleId] ON [UserRoles];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DROP INDEX [IX_ShiftRequests_ApprovedWorkScheduleId] ON [ShiftRequests];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    ALTER TABLE [RolePermissions] DROP CONSTRAINT [PK_RolePermissions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DROP INDEX [IX_RolePermissions_RoleId_PermissionId] ON [RolePermissions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DROP INDEX [IX_Doctors_SpecialtyId] ON [Doctors];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserRoles]') AND [c].[name] = N'Id');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [UserRoles] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [UserRoles] DROP COLUMN [Id];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserRoles]') AND [c].[name] = N'CreatedAt');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [UserRoles] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [UserRoles] DROP COLUMN [CreatedAt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserRoles]') AND [c].[name] = N'IsDeleted');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [UserRoles] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [UserRoles] DROP COLUMN [IsDeleted];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserRoles]') AND [c].[name] = N'UpdatedAt');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [UserRoles] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [UserRoles] DROP COLUMN [UpdatedAt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ShiftRequests]') AND [c].[name] = N'ApprovedWorkScheduleId');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [ShiftRequests] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [ShiftRequests] DROP COLUMN [ApprovedWorkScheduleId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RolePermissions]') AND [c].[name] = N'Id');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [RolePermissions] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [RolePermissions] DROP COLUMN [Id];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RolePermissions]') AND [c].[name] = N'CreatedAt');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [RolePermissions] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [RolePermissions] DROP COLUMN [CreatedAt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RolePermissions]') AND [c].[name] = N'IsDeleted');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [RolePermissions] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [RolePermissions] DROP COLUMN [IsDeleted];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RolePermissions]') AND [c].[name] = N'UpdatedAt');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [RolePermissions] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [RolePermissions] DROP COLUMN [UpdatedAt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RefreshTokens]') AND [c].[name] = N'CreatedAt');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [RefreshTokens] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [RefreshTokens] DROP COLUMN [CreatedAt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RefreshTokens]') AND [c].[name] = N'IsDeleted');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [RefreshTokens] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [RefreshTokens] DROP COLUMN [IsDeleted];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RefreshTokens]') AND [c].[name] = N'UpdatedAt');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [RefreshTokens] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [RefreshTokens] DROP COLUMN [UpdatedAt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Doctors]') AND [c].[name] = N'SpecialtyId');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Doctors] DROP CONSTRAINT ' + @var12 + ';');
    ALTER TABLE [Doctors] DROP COLUMN [SpecialtyId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    EXEC sp_rename N'[WorkSchedules].[PatientLimit]', N'PatientLimitPerSlot', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    ALTER TABLE [QueueTickets] ADD [CheckInTime] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var13 nvarchar(max);
    SELECT @var13 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Persons]') AND [c].[name] = N'Gender');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [Persons] DROP CONSTRAINT ' + @var13 + ';');
    EXEC(N'UPDATE [Persons] SET [Gender] = 0 WHERE [Gender] IS NULL');
    ALTER TABLE [Persons] ALTER COLUMN [Gender] int NOT NULL;
    ALTER TABLE [Persons] ADD DEFAULT 0 FOR [Gender];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    DECLARE @var14 nvarchar(max);
    SELECT @var14 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Persons]') AND [c].[name] = N'DateOfBirth');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [Persons] DROP CONSTRAINT ' + @var14 + ';');
    EXEC(N'UPDATE [Persons] SET [DateOfBirth] = ''0001-01-01'' WHERE [DateOfBirth] IS NULL');
    ALTER TABLE [Persons] ALTER COLUMN [DateOfBirth] date NOT NULL;
    ALTER TABLE [Persons] ADD DEFAULT '0001-01-01' FOR [DateOfBirth];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    ALTER TABLE [MedicalReports] ADD [ExamEndTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    ALTER TABLE [MedicalReports] ADD [ExamStartTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    ALTER TABLE [MedicalReports] ADD [Status] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    ALTER TABLE [Doctors] ADD [Status] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    ALTER TABLE [UserRoles] ADD CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    ALTER TABLE [RolePermissions] ADD CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'IsDeleted', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([Id], [CreatedAt], [Description], [IsDeleted], [Name], [UpdatedAt])
    VALUES (''00000000-0000-0000-0000-000000000001'', ''2026-01-01T00:00:00.0000000'', N''Administrator role'', CAST(0 AS bit), N''Admin'', NULL),
    (''00000000-0000-0000-0000-000000000002'', ''2026-01-01T00:00:00.0000000'', N''Patient role'', CAST(0 AS bit), N''Patient'', NULL),
    (''00000000-0000-0000-0000-000000000003'', ''2026-01-01T00:00:00.0000000'', N''Receptionist role'', CAST(0 AS bit), N''Receptionist'', NULL),
    (''00000000-0000-0000-0000-000000000004'', ''2026-01-01T00:00:00.0000000'', N''Doctor role'', CAST(0 AS bit), N''Doctor'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'IsDeleted', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807093426_SeedRoleData'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260807093426_SeedRoleData', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807153745_AllowNullPhoneNumber'
)
BEGIN
    DROP INDEX [IX_Persons_PhoneNumber] ON [Persons];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807153745_AllowNullPhoneNumber'
)
BEGIN
    DECLARE @var15 nvarchar(max);
    SELECT @var15 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Persons]') AND [c].[name] = N'PhoneNumber');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [Persons] DROP CONSTRAINT ' + @var15 + ';');
    ALTER TABLE [Persons] ALTER COLUMN [PhoneNumber] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807153745_AllowNullPhoneNumber'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Persons_PhoneNumber] ON [Persons] ([PhoneNumber]) WHERE [PhoneNumber] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260807153745_AllowNullPhoneNumber'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260807153745_AllowNullPhoneNumber', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810085640_SeedPermissionsAndRolePermissions'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'IsDeleted', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Permissions]'))
        SET IDENTITY_INSERT [Permissions] ON;
    EXEC(N'INSERT INTO [Permissions] ([Id], [CreatedAt], [Description], [IsDeleted], [Name], [UpdatedAt])
    VALUES (''10000000-0000-0000-0000-000000000001'', ''2026-01-01T00:00:00.0000000'', N''Create appointments'', CAST(0 AS bit), N''appointment.create'', NULL),
    (''10000000-0000-0000-0000-000000000002'', ''2026-01-01T00:00:00.0000000'', N''View appointments'', CAST(0 AS bit), N''appointment.view'', NULL),
    (''10000000-0000-0000-0000-000000000003'', ''2026-01-01T00:00:00.0000000'', N''Cancel appointments'', CAST(0 AS bit), N''appointment.cancel'', NULL),
    (''10000000-0000-0000-0000-000000000004'', ''2026-01-01T00:00:00.0000000'', N''Call next in queue'', CAST(0 AS bit), N''queue.call-next'', NULL),
    (''10000000-0000-0000-0000-000000000005'', ''2026-01-01T00:00:00.0000000'', N''View doctor profile'', CAST(0 AS bit), N''doctor.view'', NULL),
    (''10000000-0000-0000-0000-000000000006'', ''2026-01-01T00:00:00.0000000'', N''Edit doctor profile'', CAST(0 AS bit), N''doctor.edit'', NULL),
    (''10000000-0000-0000-0000-000000000007'', ''2026-01-01T00:00:00.0000000'', N''Manage users'', CAST(0 AS bit), N''user.manage'', NULL),
    (''10000000-0000-0000-0000-000000000008'', ''2026-01-01T00:00:00.0000000'', N''Manage roles and permissions'', CAST(0 AS bit), N''role.manage'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'IsDeleted', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Permissions]'))
        SET IDENTITY_INSERT [Permissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810085640_SeedPermissionsAndRolePermissions'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
        SET IDENTITY_INSERT [RolePermissions] ON;
    EXEC(N'INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
    VALUES (''10000000-0000-0000-0000-000000000001'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-000000000002'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-000000000003'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-000000000004'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-000000000005'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-000000000006'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-000000000007'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-000000000008'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-000000000001'', ''00000000-0000-0000-0000-000000000002''),
    (''10000000-0000-0000-0000-000000000002'', ''00000000-0000-0000-0000-000000000002''),
    (''10000000-0000-0000-0000-000000000003'', ''00000000-0000-0000-0000-000000000002''),
    (''10000000-0000-0000-0000-000000000001'', ''00000000-0000-0000-0000-000000000003''),
    (''10000000-0000-0000-0000-000000000002'', ''00000000-0000-0000-0000-000000000003''),
    (''10000000-0000-0000-0000-000000000003'', ''00000000-0000-0000-0000-000000000003''),
    (''10000000-0000-0000-0000-000000000004'', ''00000000-0000-0000-0000-000000000003''),
    (''10000000-0000-0000-0000-000000000002'', ''00000000-0000-0000-0000-000000000004''),
    (''10000000-0000-0000-0000-000000000005'', ''00000000-0000-0000-0000-000000000004''),
    (''10000000-0000-0000-0000-000000000006'', ''00000000-0000-0000-0000-000000000004'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
        SET IDENTITY_INSERT [RolePermissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810085640_SeedPermissionsAndRolePermissions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260810085640_SeedPermissionsAndRolePermissions', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812030720_UpdatePermissionsAndRolePermissions'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'IsDeleted', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Permissions]'))
        SET IDENTITY_INSERT [Permissions] ON;
    EXEC(N'INSERT INTO [Permissions] ([Id], [CreatedAt], [Description], [IsDeleted], [Name], [UpdatedAt])
    VALUES (''10000000-0000-0000-0000-000000000009'', ''2026-01-01T00:00:00.0000000'', N''Update appointments'', CAST(0 AS bit), N''appointment.update'', NULL),
    (''10000000-0000-0000-0000-00000000000c'', ''2026-01-01T00:00:00.0000000'', N''View doctor''''s queue'', CAST(0 AS bit), N''doctor.queue.view'', NULL),
    (''10000000-0000-0000-0000-00000000000d'', ''2026-01-01T00:00:00.0000000'', N''Create doctor profile'', CAST(0 AS bit), N''doctor.create'', NULL),
    (''1000000a-0000-0000-0000-000000000001'', ''2026-01-01T00:00:00.0000000'', N''Confirm appointments'', CAST(0 AS bit), N''appointment.confirm'', NULL),
    (''1000000a-0000-0000-0000-000000000002'', ''2026-01-01T00:00:00.0000000'', N''Reschedule appointments'', CAST(0 AS bit), N''appointment.reschedule'', NULL),
    (''20000000-0000-0000-0000-000000000001'', ''2026-01-01T00:00:00.0000000'', N''View medical reports'', CAST(0 AS bit), N''medical-report.view'', NULL),
    (''20000000-0000-0000-0000-000000000002'', ''2026-01-01T00:00:00.0000000'', N''Create medical reports'', CAST(0 AS bit), N''medical-report.create'', NULL),
    (''20000000-0000-0000-0000-000000000003'', ''2026-01-01T00:00:00.0000000'', N''View patient medical history'', CAST(0 AS bit), N''patient-history.view'', NULL),
    (''20000000-0000-0000-0000-000000000004'', ''2026-01-01T00:00:00.0000000'', N''Manage any doctor''''s shifts'', CAST(0 AS bit), N''shift.manage'', NULL),
    (''20000000-0000-0000-0000-000000000005'', ''2026-01-01T00:00:00.0000000'', N''Manage own shift requests'', CAST(0 AS bit), N''shift.self-manage'', NULL),
    (''20000000-0000-0000-0000-000000000006'', ''2026-01-01T00:00:00.0000000'', N''View queue'', CAST(0 AS bit), N''queue.view'', NULL),
    (''20000000-0000-0000-0000-000000000007'', ''2026-01-01T00:00:00.0000000'', N''Check in a patient into queue'', CAST(0 AS bit), N''queue.check-in'', NULL),
    (''20000000-0000-0000-0000-000000000008'', ''2026-01-01T00:00:00.0000000'', N''Skip a patient in queue'', CAST(0 AS bit), N''queue.skip'', NULL),
    (''20000000-0000-0000-0000-000000000009'', ''2026-01-01T00:00:00.0000000'', N''Set priority in queue'', CAST(0 AS bit), N''queue.priority'', NULL),
    (''2000000a-0000-0000-0000-000000000001'', ''2026-01-01T00:00:00.0000000'', N''Manage notifications'', CAST(0 AS bit), N''notification.manage'', NULL),
    (''2000000a-0000-0000-0000-000000000002'', ''2026-01-01T00:00:00.0000000'', N''View clinic-wide reports'', CAST(0 AS bit), N''report.view'', NULL),
    (''2000000a-0000-0000-0000-000000000003'', ''2026-01-01T00:00:00.0000000'', N''Manage medical specialties'', CAST(0 AS bit), N''specialty.manage'', NULL),
    (''2000000a-0000-0000-0000-000000000004'', ''2026-01-01T00:00:00.0000000'', N''View appointment slots'', CAST(0 AS bit), N''slot.view'', NULL),
    (''2000000a-0000-0000-0000-000000000005'', ''2026-01-01T00:00:00.0000000'', N''Manage appointment slots'', CAST(0 AS bit), N''slot.manage'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'IsDeleted', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Permissions]'))
        SET IDENTITY_INSERT [Permissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812030720_UpdatePermissionsAndRolePermissions'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
        SET IDENTITY_INSERT [RolePermissions] ON;
    EXEC(N'INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
    VALUES (''10000000-0000-0000-0000-000000000005'', ''00000000-0000-0000-0000-000000000002''),
    (''10000000-0000-0000-0000-000000000005'', ''00000000-0000-0000-0000-000000000003''),
    (''10000000-0000-0000-0000-000000000009'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-00000000000c'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-00000000000d'', ''00000000-0000-0000-0000-000000000001''),
    (''20000000-0000-0000-0000-000000000001'', ''00000000-0000-0000-0000-000000000001''),
    (''20000000-0000-0000-0000-000000000002'', ''00000000-0000-0000-0000-000000000001''),
    (''20000000-0000-0000-0000-000000000003'', ''00000000-0000-0000-0000-000000000001''),
    (''20000000-0000-0000-0000-000000000004'', ''00000000-0000-0000-0000-000000000001''),
    (''20000000-0000-0000-0000-000000000005'', ''00000000-0000-0000-0000-000000000001''),
    (''20000000-0000-0000-0000-000000000006'', ''00000000-0000-0000-0000-000000000001''),
    (''20000000-0000-0000-0000-000000000007'', ''00000000-0000-0000-0000-000000000001''),
    (''20000000-0000-0000-0000-000000000008'', ''00000000-0000-0000-0000-000000000001''),
    (''20000000-0000-0000-0000-000000000009'', ''00000000-0000-0000-0000-000000000001''),
    (''2000000a-0000-0000-0000-000000000001'', ''00000000-0000-0000-0000-000000000001''),
    (''2000000a-0000-0000-0000-000000000002'', ''00000000-0000-0000-0000-000000000001''),
    (''2000000a-0000-0000-0000-000000000003'', ''00000000-0000-0000-0000-000000000001''),
    (''2000000a-0000-0000-0000-000000000004'', ''00000000-0000-0000-0000-000000000001''),
    (''2000000a-0000-0000-0000-000000000005'', ''00000000-0000-0000-0000-000000000001''),
    (''2000000a-0000-0000-0000-000000000004'', ''00000000-0000-0000-0000-000000000002''),
    (''10000000-0000-0000-0000-000000000009'', ''00000000-0000-0000-0000-000000000003''),
    (''10000000-0000-0000-0000-00000000000c'', ''00000000-0000-0000-0000-000000000003''),
    (''20000000-0000-0000-0000-000000000004'', ''00000000-0000-0000-0000-000000000003''),
    (''20000000-0000-0000-0000-000000000006'', ''00000000-0000-0000-0000-000000000003''),
    (''20000000-0000-0000-0000-000000000007'', ''00000000-0000-0000-0000-000000000003''),
    (''20000000-0000-0000-0000-000000000008'', ''00000000-0000-0000-0000-000000000003''),
    (''20000000-0000-0000-0000-000000000009'', ''00000000-0000-0000-0000-000000000003''),
    (''2000000a-0000-0000-0000-000000000001'', ''00000000-0000-0000-0000-000000000003''),
    (''2000000a-0000-0000-0000-000000000004'', ''00000000-0000-0000-0000-000000000003''),
    (''2000000a-0000-0000-0000-000000000005'', ''00000000-0000-0000-0000-000000000003''),
    (''10000000-0000-0000-0000-000000000009'', ''00000000-0000-0000-0000-000000000004''),
    (''10000000-0000-0000-0000-00000000000c'', ''00000000-0000-0000-0000-000000000004''),
    (''20000000-0000-0000-0000-000000000001'', ''00000000-0000-0000-0000-000000000004''),
    (''20000000-0000-0000-0000-000000000002'', ''00000000-0000-0000-0000-000000000004''),
    (''20000000-0000-0000-0000-000000000003'', ''00000000-0000-0000-0000-000000000004''),
    (''20000000-0000-0000-0000-000000000005'', ''00000000-0000-0000-0000-000000000004''),
    (''20000000-0000-0000-0000-000000000006'', ''00000000-0000-0000-0000-000000000004''),
    (''2000000a-0000-0000-0000-000000000004'', ''00000000-0000-0000-0000-000000000004'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
        SET IDENTITY_INSERT [RolePermissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812030720_UpdatePermissionsAndRolePermissions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260812030720_UpdatePermissionsAndRolePermissions', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812050443_SplitAppointmentPermissionScopes'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'IsDeleted', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Permissions]'))
        SET IDENTITY_INSERT [Permissions] ON;
    EXEC(N'INSERT INTO [Permissions] ([Id], [CreatedAt], [Description], [IsDeleted], [Name], [UpdatedAt])
    VALUES (''10000000-0000-0000-0000-00000000000e'', ''2026-01-01T00:00:00.0000000'', N''View own appointments'', CAST(0 AS bit), N''appointment.view.own'', NULL),
    (''10000000-0000-0000-0000-00000000000f'', ''2026-01-01T00:00:00.0000000'', N''View any appointment'', CAST(0 AS bit), N''appointment.view.any'', NULL),
    (''10000000-0000-0000-0000-000000000010'', ''2026-01-01T00:00:00.0000000'', N''Cancel own appointment'', CAST(0 AS bit), N''appointment.cancel.own'', NULL),
    (''10000000-0000-0000-0000-000000000011'', ''2026-01-01T00:00:00.0000000'', N''Cancel any appointment'', CAST(0 AS bit), N''appointment.cancel.any'', NULL),
    (''10000000-0000-0000-0000-000000000012'', ''2026-01-01T00:00:00.0000000'', N''Update any appointment'', CAST(0 AS bit), N''appointment.update.any'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'IsDeleted', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Permissions]'))
        SET IDENTITY_INSERT [Permissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812050443_SplitAppointmentPermissionScopes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260812050443_SplitAppointmentPermissionScopes', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812092140_SplitAppointmentCancelPermissionScope'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000002'' AND [RoleId] = ''00000000-0000-0000-0000-000000000001'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812092140_SplitAppointmentCancelPermissionScope'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000003'' AND [RoleId] = ''00000000-0000-0000-0000-000000000001'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812092140_SplitAppointmentCancelPermissionScope'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000002'' AND [RoleId] = ''00000000-0000-0000-0000-000000000002'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812092140_SplitAppointmentCancelPermissionScope'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000003'' AND [RoleId] = ''00000000-0000-0000-0000-000000000002'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812092140_SplitAppointmentCancelPermissionScope'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000002'' AND [RoleId] = ''00000000-0000-0000-0000-000000000003'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812092140_SplitAppointmentCancelPermissionScope'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000003'' AND [RoleId] = ''00000000-0000-0000-0000-000000000003'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812092140_SplitAppointmentCancelPermissionScope'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000009'' AND [RoleId] = ''00000000-0000-0000-0000-000000000003'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812092140_SplitAppointmentCancelPermissionScope'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000002'' AND [RoleId] = ''00000000-0000-0000-0000-000000000004'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812092140_SplitAppointmentCancelPermissionScope'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
        SET IDENTITY_INSERT [RolePermissions] ON;
    EXEC(N'INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
    VALUES (''10000000-0000-0000-0000-00000000000f'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-000000000011'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-00000000000e'', ''00000000-0000-0000-0000-000000000002''),
    (''10000000-0000-0000-0000-000000000010'', ''00000000-0000-0000-0000-000000000002''),
    (''10000000-0000-0000-0000-00000000000f'', ''00000000-0000-0000-0000-000000000003''),
    (''10000000-0000-0000-0000-000000000011'', ''00000000-0000-0000-0000-000000000003''),
    (''10000000-0000-0000-0000-000000000012'', ''00000000-0000-0000-0000-000000000003''),
    (''10000000-0000-0000-0000-00000000000f'', ''00000000-0000-0000-0000-000000000004'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
        SET IDENTITY_INSERT [RolePermissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812092140_SplitAppointmentCancelPermissionScope'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260812092140_SplitAppointmentCancelPermissionScope', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000006'' AND [RoleId] = ''00000000-0000-0000-0000-000000000001'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000011'' AND [RoleId] = ''00000000-0000-0000-0000-000000000001'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000010'' AND [RoleId] = ''00000000-0000-0000-0000-000000000002'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000011'' AND [RoleId] = ''00000000-0000-0000-0000-000000000003'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000012'' AND [RoleId] = ''00000000-0000-0000-0000-000000000003'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''20000000-0000-0000-0000-000000000004'' AND [RoleId] = ''00000000-0000-0000-0000-000000000003'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-000000000006'' AND [RoleId] = ''00000000-0000-0000-0000-000000000004'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''10000000-0000-0000-0000-00000000000f'' AND [RoleId] = ''00000000-0000-0000-0000-000000000004'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'DELETE FROM [Permissions]
    WHERE [Id] = ''10000000-0000-0000-0000-000000000010'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'DELETE FROM [Permissions]
    WHERE [Id] = ''10000000-0000-0000-0000-000000000011'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'DELETE FROM [Permissions]
    WHERE [Id] = ''10000000-0000-0000-0000-000000000012'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'UPDATE [Permissions] SET [Description] = N''Cancel appointments (deprecated - dùng appointment.cancel.own/.any)'', [IsDeleted] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000003'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'UPDATE [Permissions] SET [Description] = N''Edit doctor profile (deprecated - dùng doctor.edit.own/.any)'', [IsDeleted] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000006'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'UPDATE [Permissions] SET [Description] = N''Cancel own appointment (Patient)'', [Name] = N''appointment.cancel.own''
    WHERE [Id] = ''10000000-0000-0000-0000-00000000000e'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    EXEC(N'UPDATE [Permissions] SET [Description] = N''Cancel any appointment (Admin/Receptionist)'', [Name] = N''appointment.cancel.any''
    WHERE [Id] = ''10000000-0000-0000-0000-00000000000f'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'IsDeleted', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Permissions]'))
        SET IDENTITY_INSERT [Permissions] ON;
    EXEC(N'INSERT INTO [Permissions] ([Id], [CreatedAt], [Description], [IsDeleted], [Name], [UpdatedAt])
    VALUES (''10000000-0000-0000-0000-000000000013'', ''2026-01-01T00:00:00.0000000'', N''Edit own doctor profile (Doctor)'', CAST(0 AS bit), N''doctor.edit.own'', NULL),
    (''10000000-0000-0000-0000-000000000014'', ''2026-01-01T00:00:00.0000000'', N''Edit any doctor profile (Admin)'', CAST(0 AS bit), N''doctor.edit.any'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'IsDeleted', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Permissions]'))
        SET IDENTITY_INSERT [Permissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
        SET IDENTITY_INSERT [RolePermissions] ON;
    EXEC(N'INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
    VALUES (''10000000-0000-0000-0000-000000000002'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-000000000002'', ''00000000-0000-0000-0000-000000000002''),
    (''10000000-0000-0000-0000-000000000002'', ''00000000-0000-0000-0000-000000000003''),
    (''10000000-0000-0000-0000-000000000009'', ''00000000-0000-0000-0000-000000000003''),
    (''1000000a-0000-0000-0000-000000000001'', ''00000000-0000-0000-0000-000000000003''),
    (''10000000-0000-0000-0000-000000000002'', ''00000000-0000-0000-0000-000000000004''),
    (''10000000-0000-0000-0000-000000000014'', ''00000000-0000-0000-0000-000000000001''),
    (''10000000-0000-0000-0000-000000000013'', ''00000000-0000-0000-0000-000000000004'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
        SET IDENTITY_INSERT [RolePermissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812102639_FixPermissionScopes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260812102639_FixPermissionScopes', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260813020113_AddPatientReschedulePermission'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''20000000-0000-0000-0000-000000000001'' AND [RoleId] = ''00000000-0000-0000-0000-000000000001'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260813020113_AddPatientReschedulePermission'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''20000000-0000-0000-0000-000000000003'' AND [RoleId] = ''00000000-0000-0000-0000-000000000001'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260813020113_AddPatientReschedulePermission'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''20000000-0000-0000-0000-000000000001'' AND [RoleId] = ''00000000-0000-0000-0000-000000000004'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260813020113_AddPatientReschedulePermission'
)
BEGIN
    EXEC(N'DELETE FROM [RolePermissions]
    WHERE [PermissionId] = ''20000000-0000-0000-0000-000000000003'' AND [RoleId] = ''00000000-0000-0000-0000-000000000004'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260813020113_AddPatientReschedulePermission'
)
BEGIN
    EXEC(N'UPDATE [Permissions] SET [IsDeleted] = CAST(1 AS bit)
    WHERE [Id] = ''20000000-0000-0000-0000-000000000001'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260813020113_AddPatientReschedulePermission'
)
BEGIN
    EXEC(N'UPDATE [Permissions] SET [IsDeleted] = CAST(1 AS bit)
    WHERE [Id] = ''20000000-0000-0000-0000-000000000003'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260813020113_AddPatientReschedulePermission'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'IsDeleted', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Permissions]'))
        SET IDENTITY_INSERT [Permissions] ON;
    EXEC(N'INSERT INTO [Permissions] ([Id], [CreatedAt], [Description], [IsDeleted], [Name], [UpdatedAt])
    VALUES (''20000000-0000-0000-0000-00000000000a'', ''2026-01-01T00:00:00.0000000'', N''View own medical reports (Patient)'', CAST(0 AS bit), N''medical-report.view.own'', NULL),
    (''20000000-0000-0000-0000-00000000000b'', ''2026-01-01T00:00:00.0000000'', N''View medical reports of patients in own exam cases (Doctor)'', CAST(0 AS bit), N''medical-report.view.related'', NULL),
    (''20000000-0000-0000-0000-00000000000c'', ''2026-01-01T00:00:00.0000000'', N''View any medical report (Admin)'', CAST(0 AS bit), N''medical-report.view.any'', NULL),
    (''20000000-0000-0000-0000-00000000000d'', ''2026-01-01T00:00:00.0000000'', N''View own medical history (Patient)'', CAST(0 AS bit), N''patient-history.view.own'', NULL),
    (''20000000-0000-0000-0000-00000000000e'', ''2026-01-01T00:00:00.0000000'', N''View medical history of patients in own exam cases (Doctor)'', CAST(0 AS bit), N''patient-history.view.related'', NULL),
    (''20000000-0000-0000-0000-00000000000f'', ''2026-01-01T00:00:00.0000000'', N''View any patient''''s medical history (Admin)'', CAST(0 AS bit), N''patient-history.view.any'', NULL),
    (''2000000a-0000-0000-0000-000000000006'', ''2026-01-01T00:00:00.0000000'', N''Approve/reject doctor shift-change suggestions (Receptionist)'', CAST(0 AS bit), N''shift.suggestion.manage'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'IsDeleted', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Permissions]'))
        SET IDENTITY_INSERT [Permissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260813020113_AddPatientReschedulePermission'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
        SET IDENTITY_INSERT [RolePermissions] ON;
    EXEC(N'INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
    VALUES (''1000000a-0000-0000-0000-000000000002'', ''00000000-0000-0000-0000-000000000002''),
    (''20000000-0000-0000-0000-00000000000c'', ''00000000-0000-0000-0000-000000000001''),
    (''20000000-0000-0000-0000-00000000000f'', ''00000000-0000-0000-0000-000000000001''),
    (''20000000-0000-0000-0000-00000000000a'', ''00000000-0000-0000-0000-000000000002''),
    (''20000000-0000-0000-0000-00000000000d'', ''00000000-0000-0000-0000-000000000002''),
    (''2000000a-0000-0000-0000-000000000006'', ''00000000-0000-0000-0000-000000000003''),
    (''20000000-0000-0000-0000-00000000000b'', ''00000000-0000-0000-0000-000000000004''),
    (''20000000-0000-0000-0000-00000000000e'', ''00000000-0000-0000-0000-000000000004'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
        SET IDENTITY_INSERT [RolePermissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260813020113_AddPatientReschedulePermission'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260813020113_AddPatientReschedulePermission', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260813034329_SeedMissingShiftSuggestionManagePermission'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260813034329_SeedMissingShiftSuggestionManagePermission', N'10.0.9');
END;

COMMIT;
GO

