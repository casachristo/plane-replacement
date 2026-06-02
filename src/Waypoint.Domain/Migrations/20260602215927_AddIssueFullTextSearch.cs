using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waypoint.Domain.Migrations;

/// <inheritdoc />
public partial class AddIssueFullTextSearch : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Generated tsvector column: title (weight A) + description (weight B). Postgres
        // computes it automatically on every insert/update — no app-side maintenance.
        // GIN index makes the @@ to_tsquery() match a sub-millisecond lookup even at
        // hundreds of thousands of rows.
        migrationBuilder.Sql("""
            ALTER TABLE issues
            ADD COLUMN search_vector tsvector
                GENERATED ALWAYS AS (
                    setweight(to_tsvector('english', coalesce(title, '')), 'A') ||
                    setweight(to_tsvector('english', coalesce(description_md, '')), 'B')
                ) STORED;
            """);
        migrationBuilder.Sql("CREATE INDEX ix_issues_search_vector ON issues USING GIN (search_vector);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_issues_search_vector;");
        migrationBuilder.Sql("ALTER TABLE issues DROP COLUMN IF EXISTS search_vector;");
    }
}
