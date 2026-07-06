using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedDogSearches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedDogSearches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdopterUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SearchText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShelterId = table.Column<int>(type: "int", nullable: true),
                    Breed = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CoatColor = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    MaxAgeYears = table.Column<int>(type: "int", nullable: true),
                    Size = table.Column<int>(type: "int", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Neighborhood = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    CatCompatibility = table.Column<int>(type: "int", nullable: true),
                    ChildrenCompatibility = table.Column<int>(type: "int", nullable: true),
                    ActivityLevel = table.Column<int>(type: "int", nullable: true),
                    ApartmentSuitability = table.Column<int>(type: "int", nullable: true),
                    SortOption = table.Column<int>(type: "int", nullable: false),
                    NearbyLabel = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    NearbyLatitude = table.Column<double>(type: "float", nullable: true),
                    NearbyLongitude = table.Column<double>(type: "float", nullable: true),
                    RadiusKm = table.Column<int>(type: "int", nullable: true),
                    CriteriaJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AlertFrequency = table.Column<int>(type: "int", nullable: false),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMatchAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedDogSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedDogSearches_AspNetUsers_AdopterUserId",
                        column: x => x.AdopterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedDogSearches_Shelters_ShelterId",
                        column: x => x.ShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SavedSearchMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SavedDogSearchId = table.Column<int>(type: "int", nullable: false),
                    DogId = table.Column<int>(type: "int", nullable: false),
                    MatchScore = table.Column<int>(type: "int", nullable: false),
                    MatchReasonsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FirstMatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DismissedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotificationSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSearchMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedSearchMatches_Dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "Dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedSearchMatches_SavedDogSearches_SavedDogSearchId",
                        column: x => x.SavedDogSearchId,
                        principalTable: "SavedDogSearches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedDogSearches_AdopterUserId",
                table: "SavedDogSearches",
                column: "AdopterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedDogSearches_AdopterUserId_Name",
                table: "SavedDogSearches",
                columns: new[] { "AdopterUserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedDogSearches_AlertsEnabled_LastEvaluatedAtUtc",
                table: "SavedDogSearches",
                columns: new[] { "AlertsEnabled", "LastEvaluatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedDogSearches_ShelterId",
                table: "SavedDogSearches",
                column: "ShelterId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearchMatches_DogId",
                table: "SavedSearchMatches",
                column: "DogId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearchMatches_SavedDogSearchId_DogId",
                table: "SavedSearchMatches",
                columns: new[] { "SavedDogSearchId", "DogId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearchMatches_Status",
                table: "SavedSearchMatches",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedSearchMatches");

            migrationBuilder.DropTable(
                name: "SavedDogSearches");
        }
    }
}
