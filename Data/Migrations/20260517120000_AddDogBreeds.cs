using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260517120000_AddDogBreeds")]
    public partial class AddDogBreeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DogBreeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DogBreeds", x => x.Id);
                });

            migrationBuilder.AddColumn<string>(
                name: "CustomBreedName",
                table: "Dogs",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DogBreedId",
                table: "Dogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMixedBreed",
                table: "Dogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            migrationBuilder.InsertData(
                table: "DogBreeds",
                columns: new[] { "Id", "CreatedAt", "IsActive", "Name" },
                columnTypes: new[] { "int", "datetime2", "bit", "nvarchar(80)" },
                values: new object[,]
                {
                    { 1, createdAt, true, "Mixed Breed" },
                    { 2, createdAt, true, "Unknown" },
                    { 3, createdAt, true, "Labrador Retriever" },
                    { 4, createdAt, true, "German Shepherd" },
                    { 5, createdAt, true, "Golden Retriever" },
                    { 6, createdAt, true, "Border Collie" },
                    { 7, createdAt, true, "Poodle" },
                    { 8, createdAt, true, "Bichon" },
                    { 9, createdAt, true, "Corgi" },
                    { 10, createdAt, true, "Spaniel" },
                    { 11, createdAt, true, "Setter" },
                    { 12, createdAt, true, "Beagle" },
                    { 13, createdAt, true, "Husky" },
                    { 14, createdAt, true, "Rottweiler" },
                    { 15, createdAt, true, "Dachshund" },
                    { 16, createdAt, true, "Chihuahua" },
                    { 17, createdAt, true, "Terrier" },
                    { 18, createdAt, true, "Boxer" },
                    { 19, createdAt, true, "Cane Corso" },
                    { 20, createdAt, true, "Belgian Malinois" },
                    { 21, createdAt, true, "Australian Shepherd" },
                    { 22, createdAt, true, "Yorkshire Terrier" },
                    { 23, createdAt, true, "Shih Tzu" },
                    { 24, createdAt, true, "Maltese" },
                    { 25, createdAt, true, "French Bulldog" },
                    { 26, createdAt, true, "Romanian Mioritic Shepherd" },
                    { 27, createdAt, true, "Romanian Carpathian Shepherd" },
                    { 28, createdAt, true, "Romanian Raven Shepherd" }
                });

            migrationBuilder.Sql(
                """
                UPDATE Dogs
                SET Breed = LTRIM(RTRIM(ISNULL(Breed, '')));

                UPDATE Dogs
                SET DogBreedId = 2,
                    IsMixedBreed = 0,
                    CustomBreedName = NULL,
                    Breed = 'Unknown'
                WHERE Breed = '' OR UPPER(Breed) IN ('UNKNOWN', 'UNSPECIFIED', 'NOT SURE');

                UPDATE Dogs
                SET DogBreedId = 1,
                    IsMixedBreed = 0,
                    CustomBreedName = NULL,
                    Breed = 'Mixed Breed'
                WHERE DogBreedId IS NULL
                    AND UPPER(Breed) IN ('MIXED', 'MIXED BREED', 'MUTT', 'CROSSBREED', 'CROSS BREED');

                UPDATE d
                SET DogBreedId = b.Id,
                    IsMixedBreed = CASE WHEN UPPER(d.Breed) LIKE '%MIX%' OR UPPER(d.Breed) LIKE '%MIXED%' THEN 1 ELSE 0 END,
                    CustomBreedName = NULL,
                    Breed = CASE WHEN UPPER(d.Breed) LIKE '%MIX%' OR UPPER(d.Breed) LIKE '%MIXED%' THEN b.Name + ' Mix' ELSE b.Name END
                FROM Dogs d
                INNER JOIN DogBreeds b ON
                    (b.Name = 'Labrador Retriever' AND UPPER(d.Breed) LIKE '%LABRADOR%') OR
                    (b.Name = 'German Shepherd' AND UPPER(d.Breed) LIKE '%GERMAN SHEPHERD%') OR
                    (b.Name = 'Golden Retriever' AND UPPER(d.Breed) LIKE '%GOLDEN RETRIEVER%') OR
                    (b.Name = 'Border Collie' AND UPPER(d.Breed) LIKE '%BORDER COLLIE%') OR
                    (b.Name = 'Poodle' AND UPPER(d.Breed) LIKE '%POODLE%') OR
                    (b.Name = 'Bichon' AND UPPER(d.Breed) LIKE '%BICHON%') OR
                    (b.Name = 'Corgi' AND UPPER(d.Breed) LIKE '%CORGI%') OR
                    (b.Name = 'Spaniel' AND UPPER(d.Breed) LIKE '%SPANIEL%') OR
                    (b.Name = 'Setter' AND UPPER(d.Breed) LIKE '%SETTER%') OR
                    (b.Name = 'Beagle' AND UPPER(d.Breed) LIKE '%BEAGLE%') OR
                    (b.Name = 'Husky' AND UPPER(d.Breed) LIKE '%HUSKY%') OR
                    (b.Name = 'Terrier' AND UPPER(d.Breed) LIKE '%TERRIER%') OR
                    (b.Name = 'Rottweiler' AND UPPER(d.Breed) LIKE '%ROTTWEILER%') OR
                    (b.Name = 'Dachshund' AND UPPER(d.Breed) LIKE '%DACHSHUND%') OR
                    (b.Name = 'Chihuahua' AND UPPER(d.Breed) LIKE '%CHIHUAHUA%') OR
                    (b.Name = 'Boxer' AND UPPER(d.Breed) LIKE '%BOXER%') OR
                    (b.Name = 'Cane Corso' AND UPPER(d.Breed) LIKE '%CANE CORSO%') OR
                    (b.Name = 'Belgian Malinois' AND UPPER(d.Breed) LIKE '%BELGIAN MALINOIS%') OR
                    (b.Name = 'Australian Shepherd' AND UPPER(d.Breed) LIKE '%AUSTRALIAN SHEPHERD%') OR
                    (b.Name = 'Yorkshire Terrier' AND UPPER(d.Breed) LIKE '%YORKSHIRE TERRIER%') OR
                    (b.Name = 'Shih Tzu' AND UPPER(d.Breed) LIKE '%SHIH TZU%') OR
                    (b.Name = 'Maltese' AND UPPER(d.Breed) LIKE '%MALTESE%') OR
                    (b.Name = 'French Bulldog' AND UPPER(d.Breed) LIKE '%FRENCH BULLDOG%') OR
                    (b.Name = 'Romanian Mioritic Shepherd' AND UPPER(d.Breed) LIKE '%MIORITIC%') OR
                    (b.Name = 'Romanian Carpathian Shepherd' AND UPPER(d.Breed) LIKE '%CARPATHIAN%') OR
                    (b.Name = 'Romanian Raven Shepherd' AND UPPER(d.Breed) LIKE '%RAVEN SHEPHERD%')
                WHERE d.DogBreedId IS NULL;

                UPDATE Dogs
                SET CustomBreedName = NULLIF(LTRIM(RTRIM(REPLACE(REPLACE(REPLACE(REPLACE(Breed, ' Mixed', ''), ' mixed', ''), ' Mix', ''), ' mix', ''))), ''),
                    IsMixedBreed = CASE WHEN UPPER(Breed) LIKE '%MIX%' OR UPPER(Breed) LIKE '%MIXED%' THEN 1 ELSE 0 END
                WHERE DogBreedId IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Dogs_DogBreedId",
                table: "Dogs",
                column: "DogBreedId");

            migrationBuilder.CreateIndex(
                name: "IX_DogBreeds_Name",
                table: "DogBreeds",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Dogs_DogBreeds_DogBreedId",
                table: "Dogs",
                column: "DogBreedId",
                principalTable: "DogBreeds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Dogs_DogBreeds_DogBreedId",
                table: "Dogs");

            migrationBuilder.DropIndex(
                name: "IX_Dogs_DogBreedId",
                table: "Dogs");

            migrationBuilder.DropTable(
                name: "DogBreeds");

            migrationBuilder.DropColumn(
                name: "CustomBreedName",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "DogBreedId",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "IsMixedBreed",
                table: "Dogs");
        }
    }
}
