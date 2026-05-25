using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260518120000_AddSecondaryDogBreed")]
    public partial class AddSecondaryDogBreed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SecondaryBreedId",
                table: "Dogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dogs_SecondaryBreedId",
                table: "Dogs",
                column: "SecondaryBreedId");

            migrationBuilder.AddForeignKey(
                name: "FK_Dogs_DogBreeds_SecondaryBreedId",
                table: "Dogs",
                column: "SecondaryBreedId",
                principalTable: "DogBreeds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Dogs_DogBreeds_SecondaryBreedId",
                table: "Dogs");

            migrationBuilder.DropIndex(
                name: "IX_Dogs_SecondaryBreedId",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "SecondaryBreedId",
                table: "Dogs");
        }
    }
}
