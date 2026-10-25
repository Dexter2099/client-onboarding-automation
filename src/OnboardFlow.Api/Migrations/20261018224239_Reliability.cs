using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnboardFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class Reliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WelcomeServiceAvailable",
                table: "Onboardings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "RegisterEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OnboardingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BriefJson = table.Column<string>(type: "jsonb", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisterEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegisterEntries_Onboardings_OnboardingId",
                        column: x => x.OnboardingId,
                        principalTable: "Onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WelcomeAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OnboardingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedVersion = table.Column<int>(type: "integer", nullable: false),
                    ExpectedAttempts = table.Column<int>(type: "integer", nullable: false),
                    Prepared = table.Column<bool>(type: "boolean", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WelcomeAttempts", x => x.Id);
                    table.CheckConstraint("CK_WelcomeAttempt_Budget", "\"Number\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_WelcomeAttempts_Onboardings_OnboardingId",
                        column: x => x.OnboardingId,
                        principalTable: "Onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegisterEntries_OnboardingId",
                table: "RegisterEntries",
                column: "OnboardingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegisterEntries_Reference",
                table: "RegisterEntries",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WelcomeAttempts_OnboardingId_CommandId",
                table: "WelcomeAttempts",
                columns: new[] { "OnboardingId", "CommandId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WelcomeAttempts_OnboardingId_Number",
                table: "WelcomeAttempts",
                columns: new[] { "OnboardingId", "Number" },
                unique: true);

            // Preserve the welcome already prepared by the main-slice schema as attempt one.
            migrationBuilder.Sql("""
                INSERT INTO "WelcomeAttempts"
                    ("Id", "OnboardingId", "Number", "CommandId", "ExpectedVersion", "ExpectedAttempts", "Prepared", "At")
                SELECT gen_random_uuid(), "Id", 1, '00000000-0000-0000-0000-000000000000'::uuid,
                    1, 0, true, "WelcomePreparedAt"
                FROM "Onboardings" WHERE "WelcomePreparedAt" IS NOT NULL;
                UPDATE "Onboardings" AS o SET "WelcomeServiceAvailable" = false
                FROM "Projects" AS p WHERE p."Id" = o."ProjectId" AND p."Reference" = 'OF-1043'
                    AND o."WelcomePreparedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegisterEntries");

            migrationBuilder.DropTable(
                name: "WelcomeAttempts");

            migrationBuilder.DropColumn(
                name: "WelcomeServiceAvailable",
                table: "Onboardings");
        }
    }
}
