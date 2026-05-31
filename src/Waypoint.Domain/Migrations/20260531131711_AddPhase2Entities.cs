using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waypoint.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_api_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "issue_intents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    intent_text = table.Column<string>(type: "text", nullable: false),
                    declared_by_token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lock_acquired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    linked_issue_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_intents", x => x.id);
                    table.ForeignKey(
                        name: "fk_issue_intents_api_tokens_declared_by_token_id",
                        column: x => x.declared_by_token_id,
                        principalTable: "api_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_intents_issues_linked_issue_id",
                        column: x => x.linked_issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_issue_intents_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "token_audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    passthrough_actor_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    passthrough_actor_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_token_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_token_audit_log_api_tokens_token_id",
                        column: x => x.token_id,
                        principalTable: "api_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_api_tokens_prefix",
                table: "api_tokens",
                column: "prefix");

            migrationBuilder.CreateIndex(
                name: "ix_issue_intents_declared_by_token_id",
                table: "issue_intents",
                column: "declared_by_token_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_intents_linked_issue_id",
                table: "issue_intents",
                column: "linked_issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_intents_lock_acquired_at",
                table: "issue_intents",
                column: "lock_acquired_at");

            migrationBuilder.CreateIndex(
                name: "ix_issue_intents_project_id_module_path",
                table: "issue_intents",
                columns: new[] { "project_id", "module_path" });

            migrationBuilder.CreateIndex(
                name: "ix_token_audit_log_token_id_at",
                table: "token_audit_log",
                columns: new[] { "token_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_intents");

            migrationBuilder.DropTable(
                name: "token_audit_log");

            migrationBuilder.DropTable(
                name: "api_tokens");
        }
    }
}
