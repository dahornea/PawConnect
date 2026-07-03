using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCopilotHistoryFeedbackExplainability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CopilotSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdopterUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    QueryText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SanitizedQuerySummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PrimaryIntent = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CompatibilityTarget = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    HomeType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ActivityLevel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Neighborhood = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UsedAiEnhancement = table.Column<bool>(type: "bit", nullable: false),
                    UsedSemanticSearch = table.Column<bool>(type: "bit", nullable: false),
                    UsedToolCalling = table.Column<bool>(type: "bit", nullable: false),
                    FallbackReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppliedConstraintsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultDogIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopilotSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CopilotSessions_AspNetUsers_AdopterUserId",
                        column: x => x.AdopterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CopilotResultFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CopilotSessionId = table.Column<int>(type: "int", nullable: false),
                    DogId = table.Column<int>(type: "int", nullable: false),
                    AdopterUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FeedbackType = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    OptionalComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WasOpened = table.Column<bool>(type: "bit", nullable: false),
                    WasFavorited = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopilotResultFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CopilotResultFeedbacks_AspNetUsers_AdopterUserId",
                        column: x => x.AdopterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CopilotResultFeedbacks_CopilotSessions_CopilotSessionId",
                        column: x => x.CopilotSessionId,
                        principalTable: "CopilotSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CopilotResultFeedbacks_Dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "Dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CopilotResultFeedbacks_AdopterUserId",
                table: "CopilotResultFeedbacks",
                column: "AdopterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CopilotResultFeedbacks_CopilotSessionId_DogId_AdopterUserId",
                table: "CopilotResultFeedbacks",
                columns: new[] { "CopilotSessionId", "DogId", "AdopterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CopilotResultFeedbacks_DogId",
                table: "CopilotResultFeedbacks",
                column: "DogId");

            migrationBuilder.CreateIndex(
                name: "IX_CopilotSessions_AdopterUserId_CreatedAt",
                table: "CopilotSessions",
                columns: new[] { "AdopterUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CopilotResultFeedbacks");

            migrationBuilder.DropTable(
                name: "CopilotSessions");
        }
    }
}
