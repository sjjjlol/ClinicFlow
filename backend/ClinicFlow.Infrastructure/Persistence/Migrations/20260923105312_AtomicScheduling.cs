using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicFlow.Migrations
{
    /// <inheritdoc />
    public partial class AtomicScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "Appointments",
                    columns: table => new
                    {
                        Id = table
                            .Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        PatientId = table.Column<int>(type: "int", nullable: false),
                        ResourceId = table.Column<int>(type: "int", nullable: false),
                        StartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        EndUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        Status = table
                            .Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Version = table.Column<int>(type: "int", nullable: false),
                        UpdatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Appointments", x => x.Id);
                        table.ForeignKey(
                            name: "FK_Appointments_Patients_PatientId",
                            column: x => x.PatientId,
                            principalTable: "Patients",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Restrict
                        );
                        table.ForeignKey(
                            name: "FK_Appointments_Resources_ResourceId",
                            column: x => x.ResourceId,
                            principalTable: "Resources",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Restrict
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "Audits",
                    columns: table => new
                    {
                        Id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        AppointmentId = table
                            .Column<string>(type: "varchar(255)", nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Action = table
                            .Column<string>(type: "longtext", nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Actor = table
                            .Column<string>(type: "longtext", nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Version = table.Column<int>(type: "int", nullable: false),
                        Summary = table
                            .Column<string>(type: "longtext", nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        CorrelationId = table
                            .Column<string>(type: "longtext", nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        AtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Audits", x => x.Id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "Idempotency",
                    columns: table => new
                    {
                        Id = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Fingerprint = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Response = table
                            .Column<string>(type: "longtext", nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Idempotency", x => x.Id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "Outbox",
                    columns: table => new
                    {
                        Id = table
                            .Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        AppointmentId = table
                            .Column<string>(type: "longtext", nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Version = table.Column<int>(type: "int", nullable: false),
                        Payload = table
                            .Column<string>(type: "longtext", nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Status = table
                            .Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Attempts = table.Column<int>(type: "int", nullable: false),
                        NextAttemptUtc = table.Column<DateTime>(
                            type: "datetime(6)",
                            nullable: false
                        ),
                        LeaseToken = table
                            .Column<string>(type: "longtext", nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        LeaseUntilUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        LastError = table
                            .Column<string>(type: "longtext", nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        CorrelationId = table
                            .Column<string>(type: "longtext", nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Outbox", x => x.Id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "SlotClaims",
                    columns: table => new
                    {
                        ResourceId = table.Column<int>(type: "int", nullable: false),
                        SlotStartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        AppointmentId = table
                            .Column<string>(type: "varchar(36)", nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_SlotClaims",
                            x => new { x.ResourceId, x.SlotStartUtc }
                        );
                        table.ForeignKey(
                            name: "FK_SlotClaims_Appointments_AppointmentId",
                            column: x => x.AppointmentId,
                            principalTable: "Appointments",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Restrict
                        );
                        table.ForeignKey(
                            name: "FK_SlotClaims_Resources_ResourceId",
                            column: x => x.ResourceId,
                            principalTable: "Resources",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Restrict
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "Tasks",
                    columns: table => new
                    {
                        Id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        AppointmentId = table
                            .Column<string>(type: "varchar(36)", nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Name = table
                            .Column<string>(type: "longtext", nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Completed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        CompletedBy = table
                            .Column<string>(type: "longtext", nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        CompletedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Tasks", x => x.Id);
                        table.ForeignKey(
                            name: "FK_Tasks_Appointments_AppointmentId",
                            column: x => x.AppointmentId,
                            principalTable: "Appointments",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Restrict
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId",
                table: "Appointments",
                column: "PatientId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ResourceId",
                table: "Appointments",
                column: "ResourceId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_StartUtc_Id",
                table: "Appointments",
                columns: new[] { "StartUtc", "Id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Audits_AppointmentId_Id",
                table: "Audits",
                columns: new[] { "AppointmentId", "Id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Status_NextAttemptUtc",
                table: "Outbox",
                columns: new[] { "Status", "NextAttemptUtc" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_SlotClaims_AppointmentId",
                table: "SlotClaims",
                column: "AppointmentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AppointmentId",
                table: "Tasks",
                column: "AppointmentId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Audits");

            migrationBuilder.DropTable(name: "Idempotency");

            migrationBuilder.DropTable(name: "Outbox");

            migrationBuilder.DropTable(name: "SlotClaims");

            migrationBuilder.DropTable(name: "Tasks");

            migrationBuilder.DropTable(name: "Appointments");
        }
    }
}
