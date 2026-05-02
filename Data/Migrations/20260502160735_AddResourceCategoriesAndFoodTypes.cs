using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PawConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceCategoriesAndFoodTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FoodTypeId",
                table: "ResourceStocks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResourceCategoryId",
                table: "ResourceStocks",
                type: "int",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<int>(
                name: "DailyFoodAmountGrams",
                table: "Dogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreferredFoodTypeId",
                table: "Dogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FoodTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceCategories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "FoodTypes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Standard dry food for adult dogs.", "Adult dry food" },
                    { 2, "Food suitable for puppies.", "Puppy food" },
                    { 3, "Food suitable for older dogs.", "Senior food" },
                    { 4, "Canned or wet dog food.", "Wet food" },
                    { 5, "Special diet food recommended by a veterinarian.", "Medical diet food" }
                });

            migrationBuilder.InsertData(
                table: "ResourceCategories",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Food supplies for dogs.", "Food" },
                    { 2, "Medication and medical supplies.", "Medicine" },
                    { 3, "Blankets and bedding materials.", "Blankets" },
                    { 4, "Cleaning and sanitation products.", "Cleaning Supplies" },
                    { 5, "Leashes, collars, bowls, and similar items.", "Accessories" },
                    { 6, "General shelter resources.", "Other" }
                });

            migrationBuilder.Sql("""
                UPDATE ResourceStocks
                SET ResourceCategoryId = 1,
                    FoodTypeId = 1,
                    Name = 'Adult Dry Food Bags'
                WHERE Name LIKE '%Food%';

                UPDATE ResourceStocks
                SET ResourceCategoryId = 2
                WHERE Name LIKE '%Medicine%';

                UPDATE ResourceStocks
                SET ResourceCategoryId = 3
                WHERE Name LIKE '%Blanket%';

                UPDATE Dogs
                SET PreferredFoodTypeId = CASE
                        WHEN Age <= 1 THEN 2
                        WHEN Age >= 5 THEN 3
                        ELSE 1
                    END,
                    DailyFoodAmountGrams = CASE
                        WHEN Size = 0 THEN 180
                        WHEN Size = 1 THEN 320
                        WHEN Size = 2 THEN 480
                        ELSE 300
                    END
                WHERE PreferredFoodTypeId IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceStocks_FoodTypeId",
                table: "ResourceStocks",
                column: "FoodTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceStocks_ResourceCategoryId",
                table: "ResourceStocks",
                column: "ResourceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Dogs_PreferredFoodTypeId",
                table: "Dogs",
                column: "PreferredFoodTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Dogs_FoodTypes_PreferredFoodTypeId",
                table: "Dogs",
                column: "PreferredFoodTypeId",
                principalTable: "FoodTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceStocks_FoodTypes_FoodTypeId",
                table: "ResourceStocks",
                column: "FoodTypeId",
                principalTable: "FoodTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceStocks_ResourceCategories_ResourceCategoryId",
                table: "ResourceStocks",
                column: "ResourceCategoryId",
                principalTable: "ResourceCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Dogs_FoodTypes_PreferredFoodTypeId",
                table: "Dogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceStocks_FoodTypes_FoodTypeId",
                table: "ResourceStocks");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceStocks_ResourceCategories_ResourceCategoryId",
                table: "ResourceStocks");

            migrationBuilder.DropTable(
                name: "FoodTypes");

            migrationBuilder.DropTable(
                name: "ResourceCategories");

            migrationBuilder.DropIndex(
                name: "IX_ResourceStocks_FoodTypeId",
                table: "ResourceStocks");

            migrationBuilder.DropIndex(
                name: "IX_ResourceStocks_ResourceCategoryId",
                table: "ResourceStocks");

            migrationBuilder.DropIndex(
                name: "IX_Dogs_PreferredFoodTypeId",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "FoodTypeId",
                table: "ResourceStocks");

            migrationBuilder.DropColumn(
                name: "ResourceCategoryId",
                table: "ResourceStocks");

            migrationBuilder.DropColumn(
                name: "DailyFoodAmountGrams",
                table: "Dogs");

            migrationBuilder.DropColumn(
                name: "PreferredFoodTypeId",
                table: "Dogs");
        }
    }
}
