using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waypoint.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cycle_id",
                table: "issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "epic_id",
                table: "issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    filename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    mime = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_attachments_comments_comment_id",
                        column: x => x.comment_id,
                        principalTable: "comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attachments_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attachments_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "components",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_components", x => x.id);
                    table.ForeignKey(
                        name: "fk_components_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_components_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cycles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cycles", x => x.id);
                    table.ForeignKey(
                        name: "fk_cycles_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "labels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    parent_label_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_labels", x => x.id);
                    table.ForeignKey(
                        name: "fk_labels_labels_parent_label_id",
                        column: x => x.parent_label_id,
                        principalTable: "labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_labels_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "issue_components",
                columns: table => new
                {
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_components", x => new { x.issue_id, x.component_id });
                    table.ForeignKey(
                        name: "fk_issue_components_components_component_id",
                        column: x => x.component_id,
                        principalTable: "components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_issue_components_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "epics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description_md = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_cycle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_epics", x => x.id);
                    table.ForeignKey(
                        name: "fk_epics_cycles_target_cycle_id",
                        column: x => x.target_cycle_id,
                        principalTable: "cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_epics_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "issue_labels",
                columns: table => new
                {
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_labels", x => new { x.issue_id, x.label_id });
                    table.ForeignKey(
                        name: "fk_issue_labels_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_issue_labels_labels_label_id",
                        column: x => x.label_id,
                        principalTable: "labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_issues_cycle_id",
                table: "issues",
                column: "cycle_id");

            migrationBuilder.CreateIndex(
                name: "ix_issues_epic_id",
                table: "issues",
                column: "epic_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_comment_id",
                table: "attachments",
                column: "comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_issue_id",
                table: "attachments",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_uploaded_by_user_id",
                table: "attachments",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_components_owner_user_id",
                table: "components",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_components_project_id_name",
                table: "components",
                columns: new[] { "project_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cycles_project_id_name",
                table: "cycles",
                columns: new[] { "project_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_epics_project_id_sequence_id",
                table: "epics",
                columns: new[] { "project_id", "sequence_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_epics_target_cycle_id",
                table: "epics",
                column: "target_cycle_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_components_component_id",
                table: "issue_components",
                column: "component_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_labels_label_id",
                table: "issue_labels",
                column: "label_id");

            migrationBuilder.CreateIndex(
                name: "ix_labels_parent_label_id",
                table: "labels",
                column: "parent_label_id");

            migrationBuilder.CreateIndex(
                name: "ix_labels_project_id_name",
                table: "labels",
                columns: new[] { "project_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_issues_cycles_cycle_id",
                table: "issues",
                column: "cycle_id",
                principalTable: "cycles",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_issues_epics_epic_id",
                table: "issues",
                column: "epic_id",
                principalTable: "epics",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_issues_cycles_cycle_id",
                table: "issues");

            migrationBuilder.DropForeignKey(
                name: "fk_issues_epics_epic_id",
                table: "issues");

            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "epics");

            migrationBuilder.DropTable(
                name: "issue_components");

            migrationBuilder.DropTable(
                name: "issue_labels");

            migrationBuilder.DropTable(
                name: "cycles");

            migrationBuilder.DropTable(
                name: "components");

            migrationBuilder.DropTable(
                name: "labels");

            migrationBuilder.DropIndex(
                name: "ix_issues_cycle_id",
                table: "issues");

            migrationBuilder.DropIndex(
                name: "ix_issues_epic_id",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "cycle_id",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "epic_id",
                table: "issues");
        }
    }
}
