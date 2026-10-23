using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Sqlserver.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleAppointmentForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_QueueTickets_Appointments_AppointmentId')
BEGIN
    ALTER TABLE [QueueTickets] DROP CONSTRAINT [FK_QueueTickets_Appointments_AppointmentId];
END;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MedicalReports_Appointments_AppointmentId')
BEGIN
    ALTER TABLE [MedicalReports] DROP CONSTRAINT [FK_MedicalReports_Appointments_AppointmentId];
END;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AppointmentSnapshots_Appointments_AppointmentId')
BEGIN
    ALTER TABLE [AppointmentSnapshots] DROP CONSTRAINT [FK_AppointmentSnapshots_Appointments_AppointmentId];
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_QueueTickets_Appointments_AppointmentId')
BEGIN
    ALTER TABLE [QueueTickets] ADD CONSTRAINT [FK_QueueTickets_Appointments_AppointmentId] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([Id]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MedicalReports_Appointments_AppointmentId')
BEGIN
    ALTER TABLE [MedicalReports] ADD CONSTRAINT [FK_MedicalReports_Appointments_AppointmentId] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([Id]);
END;
");
        }
    }
}
