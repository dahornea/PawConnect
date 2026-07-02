using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260703160000_AddMessageReports")]
public partial class AddMessageReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MessageReports",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MessageId = table.Column<int>(type: "int", nullable: false),
                ReporterUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReviewedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                AdminNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MessageReports", x => x.Id);
                table.ForeignKey(
                    name: "FK_MessageReports_AspNetUsers_ReporterUserId",
                    column: x => x.ReporterUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MessageReports_AspNetUsers_ReviewedByAdminId",
                    column: x => x.ReviewedByAdminId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_MessageReports_Messages_MessageId",
                    column: x => x.MessageId,
                    principalTable: "Messages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MessageReports_CreatedAt",
            table: "MessageReports",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_MessageReports_MessageId_ReporterUserId",
            table: "MessageReports",
            columns: new[] { "MessageId", "ReporterUserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MessageReports_ReporterUserId",
            table: "MessageReports",
            column: "ReporterUserId");

        migrationBuilder.CreateIndex(
            name: "IX_MessageReports_ReviewedByAdminId",
            table: "MessageReports",
            column: "ReviewedByAdminId");

        migrationBuilder.CreateIndex(
            name: "IX_MessageReports_Status",
            table: "MessageReports",
            column: "Status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MessageReports");
    }
}
