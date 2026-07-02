using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawConnect.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260703130000_AddMessageAttachments")]
public partial class AddMessageAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MessageAttachments",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MessageId = table.Column<int>(type: "int", nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                StoredFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                FilePathOrKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                UploadedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MessageAttachments", x => x.Id);
                table.ForeignKey(
                    name: "FK_MessageAttachments_AspNetUsers_UploadedByUserId",
                    column: x => x.UploadedByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MessageAttachments_Messages_MessageId",
                    column: x => x.MessageId,
                    principalTable: "Messages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MessageAttachments_MessageId",
            table: "MessageAttachments",
            column: "MessageId");

        migrationBuilder.CreateIndex(
            name: "IX_MessageAttachments_UploadedByUserId",
            table: "MessageAttachments",
            column: "UploadedByUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MessageAttachments");
    }
}
