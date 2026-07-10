using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationsIntelligenceHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationalInsights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fingerprint = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    AudienceType = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ShelterId = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    InsightType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceModule = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    EntityDisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    PriorityScore = table.Column<int>(type: "int", nullable: false),
                    ConfidenceLabel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    ScoreBreakdownJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RecommendedActionsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FirstDetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastDetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SnoozedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalInsights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalInsights_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalInsights_Shelters_ShelterId",
                        column: x => x.ShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalInsights_AudienceType_ShelterId_Status",
                table: "OperationalInsights",
                columns: new[] { "AudienceType", "ShelterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalInsights_AudienceType_UserId_Status",
                table: "OperationalInsights",
                columns: new[] { "AudienceType", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalInsights_Category",
                table: "OperationalInsights",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalInsights_EntityType_EntityId",
                table: "OperationalInsights",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalInsights_Fingerprint",
                table: "OperationalInsights",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalInsights_LastDetectedAtUtc",
                table: "OperationalInsights",
                column: "LastDetectedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalInsights_Severity_PriorityScore",
                table: "OperationalInsights",
                columns: new[] { "Severity", "PriorityScore" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalInsights_ShelterId",
                table: "OperationalInsights",
                column: "ShelterId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalInsights_SnoozedUntilUtc",
                table: "OperationalInsights",
                column: "SnoozedUntilUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalInsights_UserId",
                table: "OperationalInsights",
                column: "UserId");

            migrationBuilder.Sql("""
                INSERT INTO [UserSavedViews]
                    ([UserId], [Name], [PageKey], [RoleScope], [Description], [FilterStateJson], [SortStateJson], [ColumnStateJson], [ViewMode], [FilterSummaryJson], [IsPinned], [IsDefault], [IsSystemView], [CreatedAtUtc], [UpdatedAtUtc], [LastUsedAtUtc])
                VALUES
                    (NULL, N'Critical Priorities', N'Shelter.Intelligence', 2, N'Critical shelter operations priorities.', N'{"search":null,"severity":4,"category":null,"status":0}', NULL, NULL, NULL, N'["Severity: Critical","Lifecycle: Active"]', 1, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL),
                    (NULL, N'Applications Waiting Too Long', N'Shelter.Intelligence', 2, N'Delayed adoption application reviews.', N'{"search":null,"severity":null,"category":2,"status":0}', NULL, NULL, NULL, N'["Category: Application Review","Lifecycle: Active"]', 0, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL),
                    (NULL, N'Platform Risks', N'Admin.Intelligence', 1, N'High and critical platform intelligence priorities.', N'{"search":null,"severity":3,"category":null,"status":0}', NULL, NULL, NULL, N'["Severity: High","Lifecycle: Active"]', 1, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL),
                    (NULL, N'New Match Opportunities', N'Adopter.Insights', 3, N'New saved-search match opportunities.', N'{"search":null,"severity":null,"category":8,"status":0}', NULL, NULL, NULL, N'["Category: Matching","Lifecycle: Active"]', 1, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [UserSavedViews]
                WHERE [IsSystemView] = 1
                  AND [PageKey] IN (N'Shelter.Intelligence', N'Admin.Intelligence', N'Adopter.Insights');
                """);

            migrationBuilder.DropTable(
                name: "OperationalInsights");
        }
    }
}
