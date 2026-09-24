using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveTutorAgentSessionSerializationAsText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AgentSessionJson",
                table: "TutorConversations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL does not implicitly cast text back to jsonb.
            migrationBuilder.Sql("""
                ALTER TABLE "TutorConversations" ALTER COLUMN "AgentSessionJson"
                TYPE jsonb USING "AgentSessionJson"::jsonb;
                """);
        }
    }
}
