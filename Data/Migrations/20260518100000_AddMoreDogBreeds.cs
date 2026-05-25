using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260518100000_AddMoreDogBreeds")]
    public partial class AddMoreDogBreeds : Migration
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
                    (29, N'Akita'),
                    (30, N'Alaskan Malamute'),
                    (31, N'American Bulldog'),
                    (32, N'American Staffordshire Terrier'),
                    (33, N'Basenji'),
                    (34, N'Basset Hound'),
                    (35, N'Bernese Mountain Dog'),
                    (36, N'Bloodhound'),
                    (37, N'Boston Terrier'),
                    (38, N'Bulldog'),
                    (39, N'Cavalier King Charles Spaniel'),
                    (40, N'Chow Chow'),
                    (41, N'Cocker Spaniel'),
                    (42, N'Dalmatian'),
                    (43, N'Doberman Pinscher'),
                    (44, N'English Springer Spaniel'),
                    (45, N'Great Dane'),
                    (46, N'Greyhound'),
                    (47, N'Jack Russell Terrier'),
                    (48, N'Miniature Schnauzer'),
                    (49, N'Newfoundland'),
                    (50, N'Papillon'),
                    (51, N'Pekingese'),
                    (52, N'Pinscher'),
                    (53, N'Pit Bull Terrier'),
                    (54, N'Pointer'),
                    (55, N'Pomeranian'),
                    (56, N'Pug'),
                    (57, N'Samoyed'),
                    (58, N'Schnauzer'),
                    (59, N'Shar Pei'),
                    (60, N'Shetland Sheepdog'),
                    (61, N'Staffordshire Bull Terrier'),
                    (62, N'Vizsla'),
                    (63, N'Weimaraner'),
                    (64, N'West Highland White Terrier'),
                    (65, N'Whippet'),
                    (66, N'Romanian Bucovina Shepherd'),
                    (67, N'Street Dog'),
                    (68, N'Hound'),
                    (69, N'Shepherd Mix'),
                    (70, N'Retriever Mix')
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
                WHERE Id BETWEEN 29 AND 70
                    AND NOT EXISTS (SELECT 1 FROM Dogs WHERE Dogs.DogBreedId = DogBreeds.Id);
                """);
        }
    }
}
