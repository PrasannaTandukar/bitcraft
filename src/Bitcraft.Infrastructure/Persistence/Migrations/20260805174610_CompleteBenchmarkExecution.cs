using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteBenchmarkExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GenerateActualOutputs",
                table: "BenchmarkRuns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RetrievalTopK",
                table: "BenchmarkRuns",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<bool>(
                name: "UseTutorKnowledge",
                table: "BenchmarkRuns",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GenerateActualOutputs",
                table: "BenchmarkRuns");

            migrationBuilder.DropColumn(
                name: "RetrievalTopK",
                table: "BenchmarkRuns");

            migrationBuilder.DropColumn(
                name: "UseTutorKnowledge",
                table: "BenchmarkRuns");
        }
    }
}
