using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnboardFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class IntakeSpend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeSpendReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    ReservedMicros = table.Column<long>(type: "bigint", nullable: false),
                    AccountedMicros = table.Column<long>(type: "bigint", nullable: false),
                    Settled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeSpendReservations", x => x.Id);
                    table.CheckConstraint("CK_IntakeSpend_Nonnegative", "\"ReservedMicros\" > 0 AND \"AccountedMicros\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSpendReservations_Scope_Month",
                table: "IntakeSpendReservations",
                columns: new[] { "Scope", "Month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeSpendReservations");
        }
    }
}
