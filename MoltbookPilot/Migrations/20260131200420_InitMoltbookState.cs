using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoltbookPilot.Migrations
{
    /// <inheritdoc />
    public partial class InitMoltbookState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MoltbookAgentStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentHandle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgentApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoltbookAgentStates", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MoltbookAgentStates");
        }
    }
}
