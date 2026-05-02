using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddRecentlyViewedDogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecentlyViewedDogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdopterId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DogId = table.Column<int>(type: "int", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecentlyViewedDogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecentlyViewedDogs_AspNetUsers_AdopterId",
                        column: x => x.AdopterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecentlyViewedDogs_Dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "Dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecentlyViewedDogs_AdopterId_DogId",
                table: "RecentlyViewedDogs",
                columns: new[] { "AdopterId", "DogId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecentlyViewedDogs_DogId",
                table: "RecentlyViewedDogs",
                column: "DogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecentlyViewedDogs");
        }
    }
}
