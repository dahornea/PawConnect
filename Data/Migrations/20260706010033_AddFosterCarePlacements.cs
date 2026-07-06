using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFosterCarePlacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FosterCaregiverProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AddressSummary = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    PreferredShelterId = table.Column<int>(type: "int", nullable: true),
                    ExperienceNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    HomeEnvironmentNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    ActivePlacementCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FosterCaregiverProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FosterCaregiverProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FosterCaregiverProfiles_Shelters_PreferredShelterId",
                        column: x => x.PreferredShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FosterPlacements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DogId = table.Column<int>(type: "int", nullable: false),
                    ShelterId = table.Column<int>(type: "int", nullable: false),
                    FosterCaregiverProfileId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    EndedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlannedEndDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualEndDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CareInstructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MedicalNotesSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ShelterNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FosterNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FosterPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FosterPlacements_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FosterPlacements_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FosterPlacements_AspNetUsers_EndedByUserId",
                        column: x => x.EndedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FosterPlacements_Dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "Dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FosterPlacements_FosterCaregiverProfiles_FosterCaregiverProfileId",
                        column: x => x.FosterCaregiverProfileId,
                        principalTable: "FosterCaregiverProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FosterPlacements_Shelters_ShelterId",
                        column: x => x.ShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FosterPlacementActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FosterPlacementId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ActivityType = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FosterPlacementActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FosterPlacementActivities_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FosterPlacementActivities_FosterPlacements_FosterPlacementId",
                        column: x => x.FosterPlacementId,
                        principalTable: "FosterPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FosterCaregiverProfiles_IsActive",
                table: "FosterCaregiverProfiles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FosterCaregiverProfiles_PreferredShelterId",
                table: "FosterCaregiverProfiles",
                column: "PreferredShelterId");

            migrationBuilder.CreateIndex(
                name: "IX_FosterCaregiverProfiles_UserId",
                table: "FosterCaregiverProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FosterPlacementActivities_ActorUserId",
                table: "FosterPlacementActivities",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FosterPlacementActivities_CreatedAtUtc",
                table: "FosterPlacementActivities",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FosterPlacementActivities_FosterPlacementId",
                table: "FosterPlacementActivities",
                column: "FosterPlacementId");

            migrationBuilder.CreateIndex(
                name: "IX_FosterPlacements_ApprovedByUserId",
                table: "FosterPlacements",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FosterPlacements_CreatedByUserId",
                table: "FosterPlacements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FosterPlacements_DogId",
                table: "FosterPlacements",
                column: "DogId");

            migrationBuilder.CreateIndex(
                name: "IX_FosterPlacements_EndedByUserId",
                table: "FosterPlacements",
                column: "EndedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FosterPlacements_FosterCaregiverProfileId",
                table: "FosterPlacements",
                column: "FosterCaregiverProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_FosterPlacements_PlannedEndDateUtc",
                table: "FosterPlacements",
                column: "PlannedEndDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FosterPlacements_ShelterId",
                table: "FosterPlacements",
                column: "ShelterId");

            migrationBuilder.CreateIndex(
                name: "IX_FosterPlacements_StartDateUtc",
                table: "FosterPlacements",
                column: "StartDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FosterPlacements_Status",
                table: "FosterPlacements",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FosterPlacementActivities");

            migrationBuilder.DropTable(
                name: "FosterPlacements");

            migrationBuilder.DropTable(
                name: "FosterCaregiverProfiles");
        }
    }
}
