using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShelterScenarioSimulator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DogCapacity",
                table: "Shelters",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "ReservedEmergencySpaces",
                table: "Shelters",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateTable(
                name: "ShelterSimulationScenarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ShelterId = table.Column<int>(type: "int", nullable: true),
                    ScopeType = table.Column<int>(type: "int", nullable: false),
                    HorizonDays = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssumptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    IsTemplate = table.Column<bool>(type: "bit", nullable: false),
                    LastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShelterSimulationScenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShelterSimulationScenarios_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShelterSimulationScenarios_Shelters_ShelterId",
                        column: x => x.ShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShelterSimulationRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScenarioId = table.Column<int>(type: "int", nullable: true),
                    RunByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ShelterId = table.Column<int>(type: "int", nullable: true),
                    HorizonDays = table.Column<int>(type: "int", nullable: false),
                    BaselineSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssumptionsSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RiskDeltaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CapacityDeltaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecommendationSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EngineVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShelterSimulationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShelterSimulationRuns_AspNetUsers_RunByUserId",
                        column: x => x.RunByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShelterSimulationRuns_ShelterSimulationScenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "ShelterSimulationScenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShelterSimulationRuns_Shelters_ShelterId",
                        column: x => x.ShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShelterSimulationRuns_RunByUserId",
                table: "ShelterSimulationRuns",
                column: "RunByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShelterSimulationRuns_ScenarioId_CreatedAtUtc",
                table: "ShelterSimulationRuns",
                columns: new[] { "ScenarioId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ShelterSimulationRuns_ShelterId_CreatedAtUtc",
                table: "ShelterSimulationRuns",
                columns: new[] { "ShelterId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ShelterSimulationScenarios_CreatedByUserId_ScopeType_Status",
                table: "ShelterSimulationScenarios",
                columns: new[] { "CreatedByUserId", "ScopeType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ShelterSimulationScenarios_ShelterId_UpdatedAtUtc",
                table: "ShelterSimulationScenarios",
                columns: new[] { "ShelterId", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShelterSimulationRuns");

            migrationBuilder.DropTable(
                name: "ShelterSimulationScenarios");

            migrationBuilder.DropColumn(
                name: "DogCapacity",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "ReservedEmergencySpaces",
                table: "Shelters");
        }
    }
}
