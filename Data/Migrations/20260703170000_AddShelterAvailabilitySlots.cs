using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260703170000_AddShelterAvailabilitySlots")]
public partial class AddShelterAvailabilitySlots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ShelterAvailabilitySlots",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ShelterId = table.Column<int>(type: "int", nullable: false),
                StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsBooked = table.Column<bool>(type: "bit", nullable: false),
                BookedAdoptionRequestId = table.Column<int>(type: "int", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CancelledByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ShelterAvailabilitySlots", x => x.Id);
                table.ForeignKey(
                    name: "FK_ShelterAvailabilitySlots_AdoptionRequests_BookedAdoptionRequestId",
                    column: x => x.BookedAdoptionRequestId,
                    principalTable: "AdoptionRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_ShelterAvailabilitySlots_AspNetUsers_CancelledByUserId",
                    column: x => x.CancelledByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ShelterAvailabilitySlots_AspNetUsers_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ShelterAvailabilitySlots_Shelters_ShelterId",
                    column: x => x.ShelterId,
                    principalTable: "Shelters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ShelterAvailabilitySlots_BookedAdoptionRequestId",
            table: "ShelterAvailabilitySlots",
            column: "BookedAdoptionRequestId",
            unique: true,
            filter: "[BookedAdoptionRequestId] IS NOT NULL AND [IsCancelled] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_ShelterAvailabilitySlots_CancelledByUserId",
            table: "ShelterAvailabilitySlots",
            column: "CancelledByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ShelterAvailabilitySlots_CreatedByUserId",
            table: "ShelterAvailabilitySlots",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ShelterAvailabilitySlots_ShelterId_IsCancelled_IsBooked_StartTime",
            table: "ShelterAvailabilitySlots",
            columns: new[] { "ShelterId", "IsCancelled", "IsBooked", "StartTime" });

        migrationBuilder.CreateIndex(
            name: "IX_ShelterAvailabilitySlots_ShelterId_StartTime",
            table: "ShelterAvailabilitySlots",
            columns: new[] { "ShelterId", "StartTime" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ShelterAvailabilitySlots");
    }
}
