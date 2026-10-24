using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicFlow.Migrations
{
    /// <inheritdoc />
    public partial class SeparatePhysicalValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhysicalValidation",
                columns: table => new
                {
                    ReservationId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MeasuredValues = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResultAttachment = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValidationOutcome = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValidationNote = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SignedBy = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SignedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalValidation", x => x.ReservationId);
                    table.ForeignKey(
                        name: "FK_PhysicalValidation_Reservation_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
            // Copy every non-empty legacy payload, including incomplete historical records.
            migrationBuilder.Sql(@"
                INSERT INTO PhysicalValidation
                    (ReservationId, MeasuredValues, ResultAttachment, ValidationOutcome, ValidationNote, FailureType, SignedBy, SignedUtc)
                SELECT Id, MeasuredValues, ResultAttachment, ValidationOutcome, ValidationNote, FailureType, SignedBy, SignedUtc
                FROM Reservation
                WHERE MeasuredValues <> '' OR ResultAttachment <> '' OR ValidationOutcome <> ''
                    OR ValidationNote <> '' OR FailureType <> '' OR SignedBy <> '' OR SignedUtc IS NOT NULL");

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

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureType",
                table: "Reservation",
                type: "longtext",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MeasuredValues",
                table: "Reservation",
                type: "longtext",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ResultAttachment",
                table: "Reservation",
                type: "longtext",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SignedBy",
                table: "Reservation",
                type: "longtext",
                nullable: false,
                defaultValue: "")
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
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ValidationOutcome",
                table: "Reservation",
                type: "longtext",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.Sql(@"
                UPDATE Reservation r JOIN PhysicalValidation v ON v.ReservationId = r.Id
                SET r.MeasuredValues = v.MeasuredValues, r.ResultAttachment = v.ResultAttachment,
                    r.ValidationOutcome = v.ValidationOutcome, r.ValidationNote = v.ValidationNote,
                    r.FailureType = v.FailureType, r.SignedBy = v.SignedBy, r.SignedUtc = v.SignedUtc");

            migrationBuilder.DropTable(
                name: "PhysicalValidation");

        }
    }
}
