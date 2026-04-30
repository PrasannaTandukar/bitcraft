using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenBenchmarkingProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_ModelCatalogEntryId",
                table: "BenchmarkRuns",
                column: "ModelCatalogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_TutorConfigurationSnapshotId",
                table: "BenchmarkRuns",
                column: "TutorConfigurationSnapshotId");

            migrationBuilder.AddForeignKey(
                name: "FK_BenchmarkRuns_ModelCatalogEntries_ModelCatalogEntryId",
                table: "BenchmarkRuns",
                column: "ModelCatalogEntryId",
                principalTable: "ModelCatalogEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BenchmarkRuns_TutorConfigurationSnapshots_TutorConfiguratio~",
                table: "BenchmarkRuns",
                column: "TutorConfigurationSnapshotId",
                principalTable: "TutorConfigurationSnapshots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BenchmarkRuns_ModelCatalogEntries_ModelCatalogEntryId",
                table: "BenchmarkRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_BenchmarkRuns_TutorConfigurationSnapshots_TutorConfiguratio~",
                table: "BenchmarkRuns");

            migrationBuilder.DropIndex(
                name: "IX_BenchmarkRuns_ModelCatalogEntryId",
                table: "BenchmarkRuns");

            migrationBuilder.DropIndex(
                name: "IX_BenchmarkRuns_TutorConfigurationSnapshotId",
                table: "BenchmarkRuns");
        }
    }
}
