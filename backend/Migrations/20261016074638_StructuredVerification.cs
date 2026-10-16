using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicFlow.Migrations
{
    /// <inheritdoc />
    public partial class StructuredVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "TreatmentPlan",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AllowedDeviation",
                table: "TreatmentPlan",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Equipment",
                table: "TreatmentPlan",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ExpectedValues",
                table: "TreatmentPlan",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MeasurementConditions",
                table: "TreatmentPlan",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StandardName",
                table: "TreatmentPlan",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StandardVersion",
                table: "TreatmentPlan",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "VerificationMethod",
                table: "TreatmentPlan",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FailureType",
                table: "Reservation",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MeasuredValues",
                table: "Reservation",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ResultAttachment",
                table: "Reservation",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SignedBy",
                table: "Reservation",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedUtc",
                table: "Reservation",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationNote",
                table: "Reservation",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ValidationOutcome",
                table: "Reservation",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "FractionDoseGy",
                table: "Prescription",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Fractions",
                table: "Prescription",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDoseGy",
                table: "Prescription",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TreatmentMaterials",
                table: "Prescription",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TreatmentSite",
                table: "Prescription",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "Terminated",
                table: "CareCase",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TerminationReason",
                table: "CareCase",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OisDraft",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CaseId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Actor = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Operation = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Payload = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CaseVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OisDraft", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OisDraft_CaseId_Actor_Operation",
                table: "OisDraft",
                columns: new[] { "CaseId", "Actor", "Operation" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OisDraft");

            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "TreatmentPlan");

            migrationBuilder.DropColumn(
                name: "AllowedDeviation",
                table: "TreatmentPlan");

            migrationBuilder.DropColumn(
                name: "Equipment",
                table: "TreatmentPlan");

            migrationBuilder.DropColumn(
                name: "ExpectedValues",
                table: "TreatmentPlan");

            migrationBuilder.DropColumn(
                name: "MeasurementConditions",
                table: "TreatmentPlan");

            migrationBuilder.DropColumn(
                name: "StandardName",
                table: "TreatmentPlan");

            migrationBuilder.DropColumn(
                name: "StandardVersion",
                table: "TreatmentPlan");

            migrationBuilder.DropColumn(
                name: "VerificationMethod",
                table: "TreatmentPlan");

            migrationBuilder.DropColumn(
                name: "FailureType",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "MeasuredValues",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "ResultAttachment",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "SignedBy",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "SignedUtc",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "ValidationNote",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "ValidationOutcome",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "FractionDoseGy",
                table: "Prescription");

            migrationBuilder.DropColumn(
                name: "Fractions",
                table: "Prescription");

            migrationBuilder.DropColumn(
                name: "TotalDoseGy",
                table: "Prescription");

            migrationBuilder.DropColumn(
                name: "TreatmentMaterials",
                table: "Prescription");

            migrationBuilder.DropColumn(
                name: "TreatmentSite",
                table: "Prescription");

            migrationBuilder.DropColumn(
                name: "Terminated",
                table: "CareCase");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                table: "CareCase");
        }
    }
}
