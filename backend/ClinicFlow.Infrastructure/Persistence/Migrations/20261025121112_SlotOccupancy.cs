using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SlotOccupancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail before permanent DDL if legacy data cannot be represented without losing claims.
            migrationBuilder.Sql(@"
                CREATE TEMPORARY TABLE SlotOccupancyPreflight (Valid int NOT NULL CHECK (Valid = 1));
                INSERT INTO SlotOccupancyPreflight (Valid)
                SELECT 0 WHERE EXISTS (
                    SELECT 1 FROM Reservation WHERE Status <> 'Cancelled' AND
                    (EndUtc <= StartUtc OR TIMESTAMPDIFF(MICROSECOND, StartUtc, EndUtc) > 14400000000
                     OR MOD(MINUTE(StartUtc), 15) <> 0 OR SECOND(StartUtc) <> 0 OR MICROSECOND(StartUtc) <> 0
                     OR MOD(MINUTE(EndUtc), 15) <> 0 OR SECOND(EndUtc) <> 0 OR MICROSECOND(EndUtc) <> 0)
                ) OR EXISTS (
                    SELECT 1 FROM Reservation a JOIN Reservation b ON a.Id < b.Id
                    AND a.Status <> 'Cancelled' AND b.Status <> 'Cancelled'
                    AND a.StartUtc < b.EndUtc AND b.StartUtc < a.EndUtc
                    AND (a.ResourceId = b.ResourceId OR (a.PatientId IS NOT NULL AND a.PatientId = b.PatientId))
                ) OR EXISTS (
                    SELECT 1 FROM TreatmentDelivery a JOIN TreatmentDelivery b ON a.Id < b.Id
                    JOIN Reservation ar ON ar.Id = a.ReservationId JOIN Reservation br ON br.Id = b.ReservationId
                    WHERE a.Outcome = 'InProgress' AND b.Outcome = 'InProgress'
                    AND (a.MachineId = b.MachineId OR (ar.PatientId IS NOT NULL AND ar.PatientId = br.PatientId))
                );
                DROP TEMPORARY TABLE SlotOccupancyPreflight;
                ");

            migrationBuilder.CreateTable(
                name: "ActiveExecution",
                columns: table => new
                {
                    SubjectType = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubjectId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeliveryId = table.Column<string>(type: "varchar(36)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveExecution", x => new { x.SubjectType, x.SubjectId });
                    table.ForeignKey(
                        name: "FK_ActiveExecution_TreatmentDelivery_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "TreatmentDelivery",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OisCommandLock",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OisCommandLock", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReservationOccupancy",
                columns: table => new
                {
                    SubjectType = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubjectId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SlotStartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReservationId = table.Column<string>(type: "varchar(36)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationOccupancy", x => new { x.SubjectType, x.SubjectId, x.SlotStartUtc });
                    table.ForeignKey(
                        name: "FK_ReservationOccupancy_Reservation_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveExecution_DeliveryId",
                table: "ActiveExecution",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationOccupancy_ReservationId",
                table: "ReservationOccupancy",
                column: "ReservationId");

            migrationBuilder.Sql(@"
                INSERT INTO ReservationOccupancy (SubjectType, SubjectId, SlotStartUtc, ReservationId)
                SELECT 'Resource', r.ResourceId, TIMESTAMPADD(MINUTE, n.i * 15, r.StartUtc), r.Id
                FROM Reservation r CROSS JOIN
                    (SELECT 0 i UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3
                     UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7
                     UNION ALL SELECT 8 UNION ALL SELECT 9 UNION ALL SELECT 10 UNION ALL SELECT 11
                     UNION ALL SELECT 12 UNION ALL SELECT 13 UNION ALL SELECT 14 UNION ALL SELECT 15) n
                WHERE r.Status <> 'Cancelled' AND TIMESTAMPADD(MINUTE, n.i * 15, r.StartUtc) < r.EndUtc;
                INSERT INTO ReservationOccupancy (SubjectType, SubjectId, SlotStartUtc, ReservationId)
                SELECT 'Patient', CAST(r.PatientId AS CHAR), o.SlotStartUtc, r.Id
                FROM Reservation r JOIN ReservationOccupancy o ON o.ReservationId = r.Id AND o.SubjectType = 'Resource'
                WHERE r.PatientId IS NOT NULL;
                INSERT INTO ActiveExecution (SubjectType, SubjectId, DeliveryId)
                SELECT 'Resource', MachineId, Id FROM TreatmentDelivery WHERE Outcome = 'InProgress';
                INSERT INTO ActiveExecution (SubjectType, SubjectId, DeliveryId)
                SELECT 'Patient', CAST(r.PatientId AS CHAR), d.Id FROM TreatmentDelivery d
                JOIN Reservation r ON r.Id = d.ReservationId
                WHERE d.Outcome = 'InProgress' AND r.PatientId IS NOT NULL;
                ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveExecution");

            migrationBuilder.DropTable(
                name: "OisCommandLock");

            migrationBuilder.DropTable(
                name: "ReservationOccupancy");
        }
    }
}
