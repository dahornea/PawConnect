using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddShelterCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Shelters",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Shelters",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Shelters");
        }
    }
}
