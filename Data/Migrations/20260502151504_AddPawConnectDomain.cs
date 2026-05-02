using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PawConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddPawConnectDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Shelters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shelters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shelters_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Breed = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Age = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShelterId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dogs_Shelters_ShelterId",
                        column: x => x.ShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceStocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MinimumQuantity = table.Column<int>(type: "int", nullable: false),
                    ShelterId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceStocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceStocks_Shelters_ShelterId",
                        column: x => x.ShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdoptionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DogId = table.Column<int>(type: "int", nullable: false),
                    AdopterUserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdoptionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdoptionRequests_AspNetUsers_AdopterUserId",
                        column: x => x.AdopterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdoptionRequests_Dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "Dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DogImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsMainImage = table.Column<bool>(type: "bit", nullable: false),
                    DogId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DogImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DogImages_Dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "Dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FavoriteDogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DogId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteDogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FavoriteDogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FavoriteDogs_Dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "Dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VeterinarianName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DogId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalRecords_Dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "Dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Shelters",
                columns: new[] { "Id", "Address", "Description", "Email", "Name", "OwnerUserId", "PhoneNumber" },
                values: new object[] { 1, "123 Shelter Street, Bucharest", "A sample shelter used for development and demonstrations.", "shelter@pawconnect.test", "PawConnect Demo Shelter", null, "+40 700 000 001" });

            migrationBuilder.InsertData(
                table: "Dogs",
                columns: new[] { "Id", "Age", "Breed", "CreatedAt", "Description", "Name", "ShelterId", "Size", "Status" },
                values: new object[,]
                {
                    { 1, 3, "Mixed Breed", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Friendly and playful dog looking for an active family.", "Max", 1, 1, 0 },
                    { 2, 5, "Labrador Mix", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Calm, affectionate, and good with people.", "Bella", 1, 2, 1 },
                    { 3, 1, "Terrier Mix", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Young dog currently receiving basic medical care.", "Luna", 1, 0, 3 }
                });

            migrationBuilder.InsertData(
                table: "ResourceStocks",
                columns: new[] { "Id", "MinimumQuantity", "Name", "Quantity", "ShelterId", "Unit" },
                values: new object[,]
                {
                    { 1, 15, "Dry Food", 50, 1, "kg" },
                    { 2, 5, "Blankets", 20, 1, "pcs" },
                    { 3, 3, "Medicine Kits", 8, 1, "pcs" }
                });

            migrationBuilder.InsertData(
                table: "DogImages",
                columns: new[] { "Id", "Caption", "DogId", "ImageUrl", "IsMainImage" },
                values: new object[,]
                {
                    { 1, "Max main photo", 1, "https://placehold.co/800x500?text=Max", true },
                    { 2, "Bella main photo", 2, "https://placehold.co/800x500?text=Bella", true },
                    { 3, "Luna main photo", 3, "https://placehold.co/800x500?text=Luna", true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionRequests_AdopterUserId",
                table: "AdoptionRequests",
                column: "AdopterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionRequests_DogId",
                table: "AdoptionRequests",
                column: "DogId");

            migrationBuilder.CreateIndex(
                name: "IX_DogImages_DogId",
                table: "DogImages",
                column: "DogId");

            migrationBuilder.CreateIndex(
                name: "IX_Dogs_ShelterId",
                table: "Dogs",
                column: "ShelterId");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteDogs_DogId",
                table: "FavoriteDogs",
                column: "DogId");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteDogs_UserId_DogId",
                table: "FavoriteDogs",
                columns: new[] { "UserId", "DogId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecords_DogId",
                table: "MedicalRecords",
                column: "DogId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceStocks_ShelterId",
                table: "ResourceStocks",
                column: "ShelterId");

            migrationBuilder.CreateIndex(
                name: "IX_Shelters_OwnerUserId",
                table: "Shelters",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdoptionRequests");

            migrationBuilder.DropTable(
                name: "DogImages");

            migrationBuilder.DropTable(
                name: "FavoriteDogs");

            migrationBuilder.DropTable(
                name: "MedicalRecords");

            migrationBuilder.DropTable(
                name: "ResourceStocks");

            migrationBuilder.DropTable(
                name: "Dogs");

            migrationBuilder.DropTable(
                name: "Shelters");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");
        }
    }
}
