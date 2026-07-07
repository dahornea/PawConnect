using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSavedViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSavedViews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PageKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RoleScope = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FilterStateJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    SortStateJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ColumnStateJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ViewMode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    FilterSummaryJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsSystemView = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastUsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSavedViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSavedViews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSavedViews_PageKey_IsSystemView",
                table: "UserSavedViews",
                columns: new[] { "PageKey", "IsSystemView" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSavedViews_UserId",
                table: "UserSavedViews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSavedViews_UserId_PageKey",
                table: "UserSavedViews",
                columns: new[] { "UserId", "PageKey" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSavedViews_UserId_PageKey_IsDefault",
                table: "UserSavedViews",
                columns: new[] { "UserId", "PageKey", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSavedViews_UserId_PageKey_IsPinned",
                table: "UserSavedViews",
                columns: new[] { "UserId", "PageKey", "IsPinned" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSavedViews_UserId_PageKey_Name",
                table: "UserSavedViews",
                columns: new[] { "UserId", "PageKey", "Name" },
                unique: true,
                filter: "[UserId] IS NOT NULL AND [IsSystemView] = 0");

            migrationBuilder.Sql("""
                INSERT INTO [UserSavedViews]
                    ([UserId], [Name], [PageKey], [RoleScope], [Description], [FilterStateJson], [SortStateJson], [ColumnStateJson], [ViewMode], [FilterSummaryJson], [IsPinned], [IsDefault], [IsSystemView], [CreatedAtUtc], [UpdatedAtUtc], [LastUsedAtUtc])
                VALUES
                    (NULL, N'Failed Notifications', N'Admin.Notifications.Outbox', 1, N'Failed notification delivery attempts for admin review.', N'{"status":"Failed","channel":null,"type":null,"search":null}', NULL, NULL, NULL, N'["Status: Failed"]', 0, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL),
                    (NULL, N'Dead-Letter Notifications', N'Admin.Notifications.Outbox', 1, N'Notification messages that reached the dead-letter state.', N'{"status":"DeadLetter","channel":null,"type":null,"search":null}', NULL, NULL, NULL, N'["Status: Dead letter"]', 0, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL),
                    (NULL, N'Error Audit Events', N'Admin.Audit', 1, N'Audit log events with error severity.', N'{"search":null,"action":null,"entity":null,"severity":"Error","eventType":null,"fromDate":null,"toDate":null}', NULL, NULL, NULL, N'["Severity: Error"]', 0, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL),
                    (NULL, N'Dogs Needing Profile Updates', N'Shelter.Dogs', 2, N'Shelter dog profiles with incomplete or weak profile completeness data.', N'{"searchTerm":null,"status":null,"size":null,"completeness":"Needs Work","sortOption":"CompletenessAsc"}', N'{"sort":"CompletenessAsc"}', NULL, NULL, N'["Completeness: Needs Work","Sort: Completeness low-high"]', 0, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL),
                    (NULL, N'Available Small Dogs', N'Dogs.Search', 0, N'Public dog browsing preset for available small dogs.', N'{"searchTerm":null,"shelterId":null,"breed":null,"coatColor":null,"maxAge":null,"size":"Small","location":null,"neighborhood":null,"status":"Available","catCompatibility":null,"childrenCompatibility":null,"activityLevel":null,"apartmentSuitability":null,"nearbySearchTerm":null,"nearbyLabel":null,"nearbyDisplayName":null,"nearbyLatitude":null,"nearbyLongitude":null,"nearbyUsesBrowserLocation":false,"radiusKm":25,"sortOption":0}', N'{"sort":"NameAsc"}', NULL, NULL, N'["Size: Small","Status: Available","Sort: Name A-Z"]', 0, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL),
                    (NULL, N'Unread Notifications', N'Notifications.Center', 0, N'Unread notifications for the current account.', N'{"searchTerm":null,"category":null,"readState":1}', NULL, NULL, NULL, N'["Status: Unread"]', 0, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [UserSavedViews]
                WHERE [IsSystemView] = 1
                  AND [PageKey] IN (N'Admin.Notifications.Outbox', N'Admin.Audit', N'Shelter.Dogs', N'Dogs.Search', N'Notifications.Center');
                """);

            migrationBuilder.DropTable(
                name: "UserSavedViews");
        }
    }
}
