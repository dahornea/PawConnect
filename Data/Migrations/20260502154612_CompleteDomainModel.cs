using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PawConnect.Migrations
{
    /// <inheritdoc />
    public partial class CompleteDomainModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdoptionRequests_AspNetUsers_AdopterUserId",
                table: "AdoptionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FavoriteDogs_AspNetUsers_UserId",
                table: "FavoriteDogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Shelters_AspNetUsers_OwnerUserId",
                table: "Shelters");

            migrationBuilder.DropIndex(
                name: "IX_Shelters_OwnerUserId",
                table: "Shelters");

            migrationBuilder.DropIndex(
                name: "IX_AdoptionRequests_AdopterUserId",
                table: "AdoptionRequests");

            migrationBuilder.DeleteData(
                table: "DogImages",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "DogImages",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "DogImages",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "ResourceStocks",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ResourceStocks",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ResourceStocks",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Dogs",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Dogs",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Dogs",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Shelters",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DropColumn(
                name: "Title",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "Caption",
                table: "DogImages");

            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "AdoptionRequests");

            migrationBuilder.RenameColumn(
                name: "OwnerUserId",
                table: "Shelters",
                newName: "ApplicationUserId");

            migrationBuilder.RenameColumn(
                name: "MinimumQuantity",
                table: "ResourceStocks",
                newName: "LowStockThreshold");

            migrationBuilder.RenameColumn(
                name: "VeterinarianName",
                table: "MedicalRecords",
                newName: "VaccineName");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "FavoriteDogs",
                newName: "AdopterId");

            migrationBuilder.RenameIndex(
                name: "IX_FavoriteDogs_UserId_DogId",
                table: "FavoriteDogs",
                newName: "IX_FavoriteDogs_AdopterId_DogId");

            migrationBuilder.RenameColumn(
                name: "AdopterUserId",
                table: "AdoptionRequests",
                newName: "AdopterId");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Shelters",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedAt",
                table: "ResourceStocks",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<string>(
                name: "TreatmentDescription",
                table: "MedicalRecords",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "FavoriteDogs",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "BehaviorDescription",
                table: "Dogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Dogs",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MedicalStatus",
                table: "Dogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AdoptionRequests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AdoptionRequests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.CreateIndex(
                name: "IX_Shelters_ApplicationUserId",
                table: "Shelters",
                column: "ApplicationUserId",
                unique: true,
                filter: "[ApplicationUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionRequests_AdopterId_DogId",
                table: "AdoptionRequests",
                columns: new[] { "AdopterId", "DogId" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_AdoptionRequests_AspNetUsers_AdopterId",
                table: "AdoptionRequests",
                column: "AdopterId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteDogs_AspNetUsers_AdopterId",
                table: "FavoriteDogs",
                column: "AdopterId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shelters_AspNetUsers_ApplicationUserId",
                table: "Shelters",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdoptionRequests_AspNetUsers_AdopterId",
                table: "AdoptionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FavoriteDogs_AspNetUsers_AdopterId",
                table: "FavoriteDogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Shelters_AspNetUsers_ApplicationUserId",
                table: "Shelters");

            migrationBuilder.DropIndex(
                name: "IX_Shelters_ApplicationUserId",
                table: "Shelters");

            migrationBuilder.DropIndex(
                name: "IX_AdoptionRequests_AdopterId_DogId",
                table: "AdoptionRequests");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "LastUpdatedAt",
                table: "ResourceStocks");

            migrationBuilder.DropColumn(
                name: "TreatmentDescription",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "BehaviorDescription",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "MedicalStatus",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AdoptionRequests");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AdoptionRequests");

            migrationBuilder.RenameColumn(
                name: "ApplicationUserId",
                table: "Shelters",
                newName: "OwnerUserId");

            migrationBuilder.RenameColumn(
                name: "LowStockThreshold",
                table: "ResourceStocks",
                newName: "MinimumQuantity");

            migrationBuilder.RenameColumn(
                name: "VaccineName",
                table: "MedicalRecords",
                newName: "VeterinarianName");

            migrationBuilder.RenameColumn(
                name: "AdopterId",
                table: "FavoriteDogs",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_FavoriteDogs_AdopterId_DogId",
                table: "FavoriteDogs",
                newName: "IX_FavoriteDogs_UserId_DogId");

            migrationBuilder.RenameColumn(
                name: "AdopterId",
                table: "AdoptionRequests",
                newName: "AdopterUserId");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "MedicalRecords",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "FavoriteDogs",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Dogs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Caption",
                table: "DogImages",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAt",
                table: "AdoptionRequests",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

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
                name: "IX_Shelters_OwnerUserId",
                table: "Shelters",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionRequests_AdopterUserId",
                table: "AdoptionRequests",
                column: "AdopterUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdoptionRequests_AspNetUsers_AdopterUserId",
                table: "AdoptionRequests",
                column: "AdopterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteDogs_AspNetUsers_UserId",
                table: "FavoriteDogs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shelters_AspNetUsers_OwnerUserId",
                table: "Shelters",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
