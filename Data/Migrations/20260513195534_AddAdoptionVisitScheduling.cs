using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdoptionVisitScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "VisitEndTime",
                table: "Shelters",
                type: "time",
                nullable: true,
                defaultValue: new TimeSpan(0, 17, 0, 0, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "VisitStartTime",
                table: "Shelters",
                type: "time",
                nullable: true,
                defaultValue: new TimeSpan(0, 10, 0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "VisitsAllowedFriday",
                table: "Shelters",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "VisitsAllowedMonday",
                table: "Shelters",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "VisitsAllowedSaturday",
                table: "Shelters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VisitsAllowedSunday",
                table: "Shelters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VisitsAllowedThursday",
                table: "Shelters",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "VisitsAllowedTuesday",
                table: "Shelters",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "VisitsAllowedWednesday",
                table: "Shelters",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreferredVisitDateTime",
                table: "AdoptionRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VisitConfirmedAt",
                table: "AdoptionRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisitConfirmedByUserId",
                table: "AdoptionRequests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisitNotes",
                table: "AdoptionRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisitStatus",
                table: "AdoptionRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AdoptionRequests_VisitConfirmedByUserId",
                table: "AdoptionRequests",
                column: "VisitConfirmedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdoptionRequests_AspNetUsers_VisitConfirmedByUserId",
                table: "AdoptionRequests",
                column: "VisitConfirmedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdoptionRequests_AspNetUsers_VisitConfirmedByUserId",
                table: "AdoptionRequests");

            migrationBuilder.DropIndex(
                name: "IX_AdoptionRequests_VisitConfirmedByUserId",
                table: "AdoptionRequests");

            migrationBuilder.DropColumn(
                name: "VisitEndTime",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "VisitStartTime",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "VisitsAllowedFriday",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "VisitsAllowedMonday",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "VisitsAllowedSaturday",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "VisitsAllowedSunday",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "VisitsAllowedThursday",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "VisitsAllowedTuesday",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "VisitsAllowedWednesday",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "PreferredVisitDateTime",
                table: "AdoptionRequests");

            migrationBuilder.DropColumn(
                name: "VisitConfirmedAt",
                table: "AdoptionRequests");

            migrationBuilder.DropColumn(
                name: "VisitConfirmedByUserId",
                table: "AdoptionRequests");

            migrationBuilder.DropColumn(
                name: "VisitNotes",
                table: "AdoptionRequests");

            migrationBuilder.DropColumn(
                name: "VisitStatus",
                table: "AdoptionRequests");
        }
    }
}
