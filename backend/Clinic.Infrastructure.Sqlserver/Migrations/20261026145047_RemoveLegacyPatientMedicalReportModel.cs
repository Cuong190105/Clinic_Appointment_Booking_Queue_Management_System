using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Sqlserver.Migrations;

/// <summary>
/// Removes the legacy Patient and MedicalReport entities from the Clinic EF model.
/// The existing Clinic tables are intentionally retained as migration/import archives;
/// Medical Records Service owns all runtime patient and medical-record data.
/// </summary>
public partial class RemoveLegacyPatientMedicalReportModel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Intentionally no SQL: preserve existing dbo.Patients and dbo.MedicalReports data.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally no SQL: restoring EF mappings must not recreate or overwrite archive tables.
    }
}
