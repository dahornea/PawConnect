using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVolunteerTaskScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VolunteerProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PreferredShelterId = table.Column<int>(type: "int", nullable: true),
                    Skills = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AvailabilityNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolunteerProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerProfiles_Shelters_PreferredShelterId",
                        column: x => x.PreferredShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShelterId = table.Column<int>(type: "int", nullable: false),
                    DogId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AssignedVolunteerProfileId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ScheduledStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    RequiredSkills = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ShelterNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VolunteerNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolunteerTasks_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerTasks_Dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "Dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VolunteerTasks_Shelters_ShelterId",
                        column: x => x.ShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerTasks_VolunteerProfiles_AssignedVolunteerProfileId",
                        column: x => x.AssignedVolunteerProfileId,
                        principalTable: "VolunteerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerTaskActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VolunteerTaskId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ActivityType = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerTaskActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolunteerTaskActivities_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerTaskActivities_VolunteerTasks_VolunteerTaskId",
                        column: x => x.VolunteerTaskId,
                        principalTable: "VolunteerTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerProfiles_IsActive",
                table: "VolunteerProfiles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerProfiles_PreferredShelterId",
                table: "VolunteerProfiles",
                column: "PreferredShelterId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerProfiles_UserId",
                table: "VolunteerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerTaskActivities_ActorUserId",
                table: "VolunteerTaskActivities",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerTaskActivities_VolunteerTaskId_CreatedAtUtc",
                table: "VolunteerTaskActivities",
                columns: new[] { "VolunteerTaskId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerTasks_AssignedVolunteerProfileId",
                table: "VolunteerTasks",
                column: "AssignedVolunteerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerTasks_Category",
                table: "VolunteerTasks",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerTasks_CreatedByUserId",
                table: "VolunteerTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerTasks_DogId",
                table: "VolunteerTasks",
                column: "DogId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerTasks_DueAtUtc",
                table: "VolunteerTasks",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerTasks_ScheduledStartUtc",
                table: "VolunteerTasks",
                column: "ScheduledStartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerTasks_ShelterId",
                table: "VolunteerTasks",
                column: "ShelterId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerTasks_Status",
                table: "VolunteerTasks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VolunteerTaskActivities");

            migrationBuilder.DropTable(
                name: "VolunteerTasks");

            migrationBuilder.DropTable(
                name: "VolunteerProfiles");
        }
    }
}
