using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnboardFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class MinimalDemoAbuse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NetworkRunUsage",
                columns: table => new
                {
                    Network = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Starts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkRunUsage", x => new { x.Network, x.Day });
                    table.CheckConstraint("CK_NetworkRunUsage_Positive", "\"Starts\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "RevokedOwnerGrants",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevokedOwnerGrants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkRunUsage_Day",
                table: "NetworkRunUsage",
                column: "Day");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NetworkRunUsage");

            migrationBuilder.DropTable(
                name: "RevokedOwnerGrants");
        }
    }
}
