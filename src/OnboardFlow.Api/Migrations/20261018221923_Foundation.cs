using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnboardFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SandboxSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SandboxSessions", x => x.Id);
                    table.CheckConstraint("CK_SandboxSession_Lifetime", "\"ExpiresAt\" = \"CreatedAt\" + interval '24 hours'");
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SandboxSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ClientName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ClientContact = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Owner = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Service = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SignedPages = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_SandboxSessions_SandboxSessionId",
                        column: x => x.SandboxSessionId,
                        principalTable: "SandboxSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_SandboxSessionId_Reference",
                table: "Projects",
                columns: new[] { "SandboxSessionId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SandboxSessions_ExpiresAt",
                table: "SandboxSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SandboxSessions_TokenHash",
                table: "SandboxSessions",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "SandboxSessions");
        }
    }
}
