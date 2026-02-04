using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoltbookPilot.Migrations
{
    /// <inheritdoc />
    public partial class Comments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommentId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PostId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RepliedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedComments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedComments_CommentId",
                table: "ProcessedComments",
                column: "CommentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedComments");
        }
    }
}
