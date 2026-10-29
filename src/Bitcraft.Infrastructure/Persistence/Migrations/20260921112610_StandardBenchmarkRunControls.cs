using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StandardBenchmarkRunControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledUtc",
                table: "StandardBenchmarkRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EngineRunId",
                table: "StandardBenchmarkRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "StandardBenchmarkRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledUtc",
                table: "StandardBenchmarkRuns");

            migrationBuilder.DropColumn(
                name: "EngineRunId",
                table: "StandardBenchmarkRuns");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "StandardBenchmarkRuns");
        }
    }
}
