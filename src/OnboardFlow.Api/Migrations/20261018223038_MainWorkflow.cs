using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnboardFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class MainWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Onboardings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    WelcomePreparedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReadyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewTaskClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BriefJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Onboardings", x => x.Id);
                    table.CheckConstraint("CK_Onboarding_ReadySnapshot", "(\"ReadyAt\" IS NULL AND \"BriefJson\" IS NULL AND \"ReviewTaskClosedAt\" IS NULL) OR (\"ReadyAt\" IS NOT NULL AND \"BriefJson\" IS NOT NULL AND \"ReviewTaskClosedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Onboardings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OnboardingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Actor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityEvents_Onboardings_OnboardingId",
                        column: x => x.OnboardingId,
                        principalTable: "Onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OnboardingId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Subject = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PreparedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboxMessages_Onboardings_OnboardingId",
                        column: x => x.OnboardingId,
                        principalTable: "Onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OnboardingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestItems", x => x.Id);
                    table.CheckConstraint("CK_RequestItem_Kind", "\"Kind\" IN ('goals', 'pages', 'assets')");
                    table.ForeignKey(
                        name: "FK_RequestItems_Onboardings_OnboardingId",
                        column: x => x.OnboardingId,
                        principalTable: "Onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    SubmissionKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AnswerJson = table.Column<string>(type: "jsonb", nullable: false),
                    SubmittedBy = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemRevisions", x => x.Id);
                    table.CheckConstraint("CK_ItemRevision_Number", "\"Number\" > 0");
                    table.CheckConstraint("CK_ItemRevision_Review", "(\"Decision\" IS NULL AND \"Comment\" IS NULL AND \"ReviewedBy\" IS NULL AND \"ReviewedAt\" IS NULL) OR (\"Decision\" IS NOT NULL AND \"ReviewedBy\" IS NOT NULL AND \"ReviewedAt\" IS NOT NULL AND ((\"Decision\" = 'approved' AND \"Comment\" IS NULL) OR (\"Decision\" = 'changes_requested' AND \"Comment\" IS NOT NULL AND length(btrim(\"Comment\")) > 0)))");
                    table.ForeignKey(
                        name: "FK_ItemRevisions_RequestItems_RequestItemId",
                        column: x => x.RequestItemId,
                        principalTable: "RequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_OnboardingId_Sequence",
                table: "ActivityEvents",
                columns: new[] { "OnboardingId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemRevisions_RequestItemId_Number",
                table: "ItemRevisions",
                columns: new[] { "RequestItemId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemRevisions_RequestItemId_SubmissionKey",
                table: "ItemRevisions",
                columns: new[] { "RequestItemId", "SubmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_ProjectId",
                table: "Onboardings",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_RequestId",
                table: "Onboardings",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_ReviewTaskId",
                table: "Onboardings",
                column: "ReviewTaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_OnboardingId_MessageKey",
                table: "OutboxMessages",
                columns: new[] { "OnboardingId", "MessageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestItems_OnboardingId_Kind",
                table: "RequestItems",
                columns: new[] { "OnboardingId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityEvents");

            migrationBuilder.DropTable(
                name: "ItemRevisions");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "RequestItems");

            migrationBuilder.DropTable(
                name: "Onboardings");
        }
    }
}
