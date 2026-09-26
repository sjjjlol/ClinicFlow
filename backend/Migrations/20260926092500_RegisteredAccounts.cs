using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicFlow.Migrations
{
    /// <inheritdoc />
    public partial class RegisteredAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PatientId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PatientId",
                table: "Users",
                column: "PatientId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Patients_PatientId",
                table: "Users",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Patients_PatientId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PatientId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PatientId",
                table: "Users");
        }
    }
}
