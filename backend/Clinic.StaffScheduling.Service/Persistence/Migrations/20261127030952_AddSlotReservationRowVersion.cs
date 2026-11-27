using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.StaffScheduling.Service.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlotReservationRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SlotReservations",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SlotReservations");
        }
    }
}
