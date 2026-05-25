using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260518103000_AddEvenMoreDogBreeds")]
    public partial class AddEvenMoreDogBreeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @CreatedAt datetime2 = '2026-01-01T00:00:00.0000000';

                SET IDENTITY_INSERT DogBreeds ON;

                INSERT INTO DogBreeds (Id, Name, IsActive, CreatedAt)
                SELECT v.Id, v.Name, 1, @CreatedAt
                FROM (VALUES
                    (71, N'Saint Bernard'),
                    (72, N'Rhodesian Ridgeback'),
                    (73, N'Collie'),
                    (74, N'Japanese Spitz'),
                    (75, N'Spitz'),
                    (76, N'English Cocker Spaniel'),
                    (77, N'Presa Canario'),
                    (78, N'Argentine Dogo'),
                    (79, N'Kangal Shepherd'),
                    (80, N'Afghan Hound'),
                    (81, N'Airedale Terrier'),
                    (82, N'Borzoi'),
                    (83, N'Brittany Spaniel'),
                    (84, N'Bull Terrier'),
                    (85, N'Bullmastiff'),
                    (86, N'Chinese Crested'),
                    (87, N'Irish Setter'),
                    (88, N'Irish Wolfhound'),
                    (89, N'Italian Greyhound'),
                    (90, N'Lhasa Apso'),
                    (91, N'Miniature Pinscher'),
                    (92, N'Old English Sheepdog'),
                    (93, N'Portuguese Water Dog'),
                    (94, N'Scottish Terrier'),
                    (95, N'Soft Coated Wheaten Terrier'),
                    (96, N'Welsh Corgi'),
                    (97, N'English Bulldog'),
                    (98, N'Siberian Husky'),
                    (99, N'Toy Poodle'),
                    (100, N'Miniature Poodle')
                ) AS v(Id, Name)
                WHERE NOT EXISTS (SELECT 1 FROM DogBreeds existing WHERE existing.Id = v.Id)
                    AND NOT EXISTS (SELECT 1 FROM DogBreeds existing WHERE existing.Name = v.Name);

                SET IDENTITY_INSERT DogBreeds OFF;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM DogBreeds
                WHERE Id BETWEEN 71 AND 100
                    AND NOT EXISTS (SELECT 1 FROM Dogs WHERE Dogs.DogBreedId = DogBreeds.Id);
                """);
        }
    }
}
