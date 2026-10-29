using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StandardBenchmarkProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompletedTasks",
                table: "StandardBenchmarkRuns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ProgressPercent",
                table: "StandardBenchmarkRuns",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalTasks",
                table: "StandardBenchmarkRuns",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedTasks",
                table: "StandardBenchmarkRuns");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "StandardBenchmarkRuns");

            migrationBuilder.DropColumn(
                name: "TotalTasks",
                table: "StandardBenchmarkRuns");
        }
    }
}
