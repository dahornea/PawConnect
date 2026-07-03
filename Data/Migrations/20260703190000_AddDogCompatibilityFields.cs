using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDogCompatibilityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActivityLevel",
                table: "Dogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ApartmentSuitability",
                table: "Dogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CatCompatibility",
                table: "Dogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChildrenCompatibility",
                table: "Dogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CompatibilityNotes",
                table: "Dogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DogCompatibility",
                table: "Dogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExperienceNeeded",
                table: "Dogs",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityLevel",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "ApartmentSuitability",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "CatCompatibility",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "ChildrenCompatibility",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "CompatibilityNotes",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "DogCompatibility",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "ExperienceNeeded",
                table: "Dogs");
        }
    }
}
