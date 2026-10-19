using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Sqlserver.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261019150000_LinkMedicalReportsToAppointments")]
public sealed class LinkMedicalReportsToAppointments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_MedicalReports_QueueTickets_QueueTicketId",
            table: "MedicalReports");

        migrationBuilder.DropIndex(
            name: "IX_MedicalReports_QueueTicketId",
            table: "MedicalReports");

        migrationBuilder.AddColumn<Guid>(
            name: "AppointmentId",
            table: "MedicalReports",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PatientId",
            table: "MedicalReports",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE report
            SET report.AppointmentId = ticket.AppointmentId,
                report.PatientId = appointment.PatientId
            FROM MedicalReports AS report
            INNER JOIN QueueTickets AS ticket ON ticket.Id = report.QueueTicketId
            INNER JOIN Appointments AS appointment ON appointment.Id = ticket.AppointmentId;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "AppointmentId",
            table: "MedicalReports",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "PatientId",
            table: "MedicalReports",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "QueueTicketId",
            table: "MedicalReports");

        migrationBuilder.CreateIndex(
            name: "IX_MedicalReports_AppointmentId",
            table: "MedicalReports",
            column: "AppointmentId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MedicalReports_PatientId",
            table: "MedicalReports",
            column: "PatientId");

        migrationBuilder.AddForeignKey(
            name: "FK_MedicalReports_Appointments_AppointmentId",
            table: "MedicalReports",
            column: "AppointmentId",
            principalTable: "Appointments",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_MedicalReports_Patients_PatientId",
            table: "MedicalReports",
            column: "PatientId",
            principalTable: "Patients",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_MedicalReports_Appointments_AppointmentId",
            table: "MedicalReports");

        migrationBuilder.DropForeignKey(
            name: "FK_MedicalReports_Patients_PatientId",
            table: "MedicalReports");

        migrationBuilder.DropIndex(
            name: "IX_MedicalReports_AppointmentId",
            table: "MedicalReports");

        migrationBuilder.DropIndex(
            name: "IX_MedicalReports_PatientId",
            table: "MedicalReports");

        migrationBuilder.AddColumn<Guid>(
            name: "QueueTicketId",
            table: "MedicalReports",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE report
            SET report.QueueTicketId = ticket.Id
            FROM MedicalReports AS report
            INNER JOIN QueueTickets AS ticket ON ticket.AppointmentId = report.AppointmentId;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "QueueTicketId",
            table: "MedicalReports",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.DropColumn(name: "AppointmentId", table: "MedicalReports");
        migrationBuilder.DropColumn(name: "PatientId", table: "MedicalReports");

        migrationBuilder.CreateIndex(
            name: "IX_MedicalReports_QueueTicketId",
            table: "MedicalReports",
            column: "QueueTicketId",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_MedicalReports_QueueTickets_QueueTicketId",
            table: "MedicalReports",
            column: "QueueTicketId",
            principalTable: "QueueTickets",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
