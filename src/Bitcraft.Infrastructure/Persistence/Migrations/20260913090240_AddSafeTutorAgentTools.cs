using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations;

/// <summary>
/// Restored historical migration for databases that applied the Prompt 6 safe-tool schema.
/// The migration identity must remain available so EF can match it to existing migration history.
/// </summary>
[Migration("20260913090240_AddSafeTutorAgentTools")]
[DbContext(typeof(AppDbContext))]
public partial class AddSafeTutorAgentTools : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ExecutionPolicy",
            table: "ToolDefinitions",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "ImplementationKey",
            table: "ToolDefinitions",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "Kind",
            table: "ToolDefinitions",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddCheckConstraint(
            name: "CK_ToolDefinitions_ActiveImplementation",
            table: "ToolDefinitions",
            sql: "\"ExecutionPolicy\" = 0 OR length(\"ImplementationKey\") > 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ToolDefinitions_ExecutionPolicy",
            table: "ToolDefinitions",
            sql: "\"ExecutionPolicy\" IN (0, 1, 2)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ToolDefinitions_Kind",
            table: "ToolDefinitions",
            sql: "\"Kind\" IN (0, 1, 2)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_ToolDefinitions_ActiveImplementation",
            table: "ToolDefinitions");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ToolDefinitions_ExecutionPolicy",
            table: "ToolDefinitions");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ToolDefinitions_Kind",
            table: "ToolDefinitions");

        migrationBuilder.DropColumn(
            name: "ExecutionPolicy",
            table: "ToolDefinitions");

        migrationBuilder.DropColumn(
            name: "ImplementationKey",
            table: "ToolDefinitions");

        migrationBuilder.DropColumn(
            name: "Kind",
            table: "ToolDefinitions");
    }
}
