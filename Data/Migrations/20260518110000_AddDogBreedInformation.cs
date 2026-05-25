using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260518110000_AddDogBreedInformation")]
    public partial class AddDogBreedInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CareNotes",
                table: "DogBreeds",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneralDescription",
                table: "DogBreeds",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypicalTraits",
                table: "DogBreeds",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE DogBreeds SET
                    GeneralDescription = N'Mixed-breed dogs can combine traits from several backgrounds, so their behavior may be less predictable from breed labels alone.',
                    TypicalTraits = N'Traits can vary widely between individual dogs.',
                    CareNotes = N'Use the dog''s shelter notes, observed behavior, and history as the main guide.'
                WHERE Name = N'Mixed Breed';

                UPDATE DogBreeds SET
                    GeneralDescription = N'This dog''s breed background is not known.',
                    TypicalTraits = N'Breed-based expectations are limited.',
                    CareNotes = N'Rely on the dog''s description, behavior notes, and shelter observations.'
                WHERE Name = N'Unknown';

                UPDATE DogBreeds SET
                    GeneralDescription = N'Labrador Retriever-type dogs are often social, trainable, and people-oriented.',
                    TypicalTraits = N'Often sociable, food-motivated, and eager to learn.',
                    CareNotes = N'They may enjoy regular exercise and enrichment; check the dog''s own activity level before assuming fit.'
                WHERE Name = N'Labrador Retriever';

                UPDATE DogBreeds SET
                    GeneralDescription = N'Border Collie-type dogs are commonly attentive, quick learners, and responsive to structure.',
                    TypicalTraits = N'Often alert, intelligent, and motivated by activity or training games.',
                    CareNotes = N'Some may need more mental stimulation than a casual household expects, so individual energy level matters.'
                WHERE Name = N'Border Collie';

                UPDATE DogBreeds SET
                    GeneralDescription = N'Poodle-type dogs are often bright, people-focused, and adaptable when their routine is clear.',
                    TypicalTraits = N'Commonly trainable, observant, and responsive to gentle guidance.',
                    CareNotes = N'Coat care can be important, and individual confidence around new people can vary.'
                WHERE Name = N'Poodle';

                UPDATE DogBreeds SET
                    GeneralDescription = N'Bichon-type dogs are often companionable and may enjoy being close to people.',
                    TypicalTraits = N'Commonly cheerful, social, and suited to steady indoor routines.',
                    CareNotes = N'Regular coat care is usually needed, and shelter behavior should guide expectations.'
                WHERE Name = N'Bichon';

                UPDATE DogBreeds SET
                    GeneralDescription = N'Corgi-type dogs are often expressive, sturdy small dogs with a confident personality.',
                    TypicalTraits = N'May be alert, people-oriented, and responsive to routine.',
                    CareNotes = N'Some can be vocal or opinionated, so check the dog''s individual notes and handling style.'
                WHERE Name = N'Corgi';

                UPDATE DogBreeds SET
                    GeneralDescription = N'Spaniel-type dogs are often affectionate, people-oriented, and interested in gentle activity.',
                    TypicalTraits = N'Commonly friendly, curious, and responsive to positive handling.',
                    CareNotes = N'Energy and confidence vary by dog, so shelter observations are more useful than breed expectations alone.'
                WHERE Name = N'Spaniel';

                UPDATE DogBreeds SET
                    GeneralDescription = N'Setter-type dogs are often gentle, social, and interested in outdoor exploration.',
                    TypicalTraits = N'May be friendly, active, and affectionate with familiar people.',
                    CareNotes = N'They can need regular movement, but the individual dog''s age and shelter behavior are more important.'
                WHERE Name = N'Setter';

                UPDATE DogBreeds SET
                    GeneralDescription = N'German Shepherd-type dogs are often loyal, observant, and responsive to consistent training.',
                    TypicalTraits = N'Commonly intelligent, attentive, and protective of familiar routines.',
                    CareNotes = N'They may need structure and confident handling, so individual temperament should be reviewed carefully.'
                WHERE Name = N'German Shepherd';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CareNotes",
                table: "DogBreeds");

            migrationBuilder.DropColumn(
                name: "GeneralDescription",
                table: "DogBreeds");

            migrationBuilder.DropColumn(
                name: "TypicalTraits",
                table: "DogBreeds");
        }
    }
}
