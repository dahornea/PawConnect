using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDogTransferRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DogTransferRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DogId = table.Column<int>(type: "int", nullable: false),
                    SourceShelterId = table.Column<int>(type: "int", nullable: false),
                    DestinationShelterId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RespondedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CompletedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceShelterNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DestinationShelterResponseNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DogTransferRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DogTransferRequests_AspNetUsers_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DogTransferRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DogTransferRequests_AspNetUsers_RespondedByUserId",
                        column: x => x.RespondedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DogTransferRequests_Dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "Dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DogTransferRequests_Shelters_DestinationShelterId",
                        column: x => x.DestinationShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DogTransferRequests_Shelters_SourceShelterId",
                        column: x => x.SourceShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DogTransferRequests_CompletedByUserId",
                table: "DogTransferRequests",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DogTransferRequests_DestinationShelterId",
                table: "DogTransferRequests",
                column: "DestinationShelterId");

            migrationBuilder.CreateIndex(
                name: "IX_DogTransferRequests_DogId",
                table: "DogTransferRequests",
                column: "DogId");

            migrationBuilder.CreateIndex(
                name: "IX_DogTransferRequests_RequestedAtUtc",
                table: "DogTransferRequests",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DogTransferRequests_RequestedByUserId",
                table: "DogTransferRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DogTransferRequests_RespondedByUserId",
                table: "DogTransferRequests",
                column: "RespondedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DogTransferRequests_SourceShelterId",
                table: "DogTransferRequests",
                column: "SourceShelterId");

            migrationBuilder.CreateIndex(
                name: "IX_DogTransferRequests_Status",
                table: "DogTransferRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DogTransferRequests");
        }
    }
}
