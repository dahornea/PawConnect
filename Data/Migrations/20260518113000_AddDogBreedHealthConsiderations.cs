using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260518113000_AddDogBreedHealthConsiderations")]
    public partial class AddDogBreedHealthConsiderations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommonHealthConsiderations",
                table: "DogBreeds",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE DogBreeds SET CommonHealthConsiderations = N'Mixed-breed dogs can have varied traits and health backgrounds. Individual medical records and shelter observations are the most reliable source of information.'
                WHERE Name = N'Mixed Breed';

                UPDATE DogBreeds SET CommonHealthConsiderations = N'No breed-specific health note is available. Please rely on this dog''s medical records and shelter or veterinary information.'
                WHERE Name = N'Unknown';

                UPDATE DogBreeds SET CommonHealthConsiderations = N'Labrador-type dogs may be more prone to joint issues, weight gain, and ear problems. This does not mean this dog has these conditions; review the medical records and ask the shelter or veterinarian.'
                WHERE Name = N'Labrador Retriever';

                UPDATE DogBreeds SET CommonHealthConsiderations = N'Border Collie-type dogs can be associated with high exercise needs and may benefit from mental enrichment. Some lines may be prone to eye or joint issues, so medical records should be reviewed.'
                WHERE Name = N'Border Collie';

                UPDATE DogBreeds SET CommonHealthConsiderations = N'Poodle-type dogs may need regular coat and ear care. Some may be prone to dental, eye, or joint issues depending on size and background.'
                WHERE Name = N'Poodle';

                UPDATE DogBreeds SET CommonHealthConsiderations = N'Bichon-type dogs may need regular coat care and can be prone to dental or skin sensitivities. Medical records and shelter observations are more important than breed expectations.'
                WHERE Name = N'Bichon';

                UPDATE DogBreeds SET CommonHealthConsiderations = N'Corgi-type dogs may be more prone to back or joint strain because of their body shape. Weight management and appropriate exercise can be important.'
                WHERE Name = N'Corgi';

                UPDATE DogBreeds SET CommonHealthConsiderations = N'Spaniel-type dogs may be more prone to ear issues because of ear shape. Coat and ear care should be discussed with the shelter or veterinarian.'
                WHERE Name = N'Spaniel';

                UPDATE DogBreeds SET CommonHealthConsiderations = N'Setter-type dogs can be associated with higher exercise needs and may be prone to ear or joint considerations. Review this dog''s medical records for actual history.'
                WHERE Name = N'Setter';

                UPDATE DogBreeds SET CommonHealthConsiderations = N'German Shepherd-type dogs may be more prone to hip or elbow issues and may benefit from structured exercise and weight management.'
                WHERE Name = N'German Shepherd';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommonHealthConsiderations",
                table: "DogBreeds");
        }
    }
}
