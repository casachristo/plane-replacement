using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waypoint.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddGateOverrideEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gate_override_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gate_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    actor_type = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gate_override_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_gate_override_events_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gate_override_events_gate_name_at",
                table: "gate_override_events",
                columns: new[] { "gate_name", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_gate_override_events_issue_id",
                table: "gate_override_events",
                column: "issue_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gate_override_events");
        }
    }
}
