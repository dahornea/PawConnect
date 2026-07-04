using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLostFoundPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LostFoundPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DogName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    BreedText = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Size = table.Column<int>(type: "int", nullable: true),
                    CoatColor = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DistinctiveMarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastSeenOrFoundDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Neighborhood = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    AddressOrAreaDescription = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ContactInfoPublic = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostFoundPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostFoundPosts_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostFoundPosts_AspNetUsers_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostFoundPosts_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LostFoundPostImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LostFoundPostId = table.Column<int>(type: "int", nullable: false),
                    ImageUrlOrPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsMain = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostFoundPostImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostFoundPostImages_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostFoundPostImages_LostFoundPosts_LostFoundPostId",
                        column: x => x.LostFoundPostId,
                        principalTable: "LostFoundPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundPostImages_LostFoundPostId",
                table: "LostFoundPostImages",
                column: "LostFoundPostId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundPostImages_UploadedByUserId",
                table: "LostFoundPostImages",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundPosts_ApprovedByUserId",
                table: "LostFoundPosts",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundPosts_City",
                table: "LostFoundPosts",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundPosts_ClosedByUserId",
                table: "LostFoundPosts",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundPosts_CreatedByUserId",
                table: "LostFoundPosts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundPosts_LastSeenOrFoundDate",
                table: "LostFoundPosts",
                column: "LastSeenOrFoundDate");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundPosts_Neighborhood",
                table: "LostFoundPosts",
                column: "Neighborhood");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundPosts_PostType",
                table: "LostFoundPosts",
                column: "PostType");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundPosts_Status",
                table: "LostFoundPosts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LostFoundPostImages");

            migrationBuilder.DropTable(
                name: "LostFoundPosts");
        }
    }
}
