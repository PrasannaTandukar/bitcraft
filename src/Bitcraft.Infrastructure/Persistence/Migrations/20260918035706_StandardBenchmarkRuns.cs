using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StandardBenchmarkRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StandardBenchmarkRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InteractionModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    BenchmarkKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TasksJson = table.Column<string>(type: "jsonb", nullable: true),
                    NShots = table.Column<int>(type: "integer", nullable: false),
                    NProblems = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    QueuedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EngineName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EngineVersion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    OverallScore = table.Column<double>(type: "double precision", nullable: true),
                    TaskCount = table.Column<int>(type: "integer", nullable: false),
                    TaskScoresJson = table.Column<string>(type: "jsonb", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FailureDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardBenchmarkRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StandardBenchmarkRuns_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StandardBenchmarkRuns_AspNetUsers_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StandardBenchmarkRuns_Models_InteractionModelId",
                        column: x => x.InteractionModelId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StandardBenchmarkRuns_BenchmarkKey_QueuedUtc",
                table: "StandardBenchmarkRuns",
                columns: new[] { "BenchmarkKey", "QueuedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StandardBenchmarkRuns_CreatedById",
                table: "StandardBenchmarkRuns",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_StandardBenchmarkRuns_InteractionModelId",
                table: "StandardBenchmarkRuns",
                column: "InteractionModelId");

            migrationBuilder.CreateIndex(
                name: "IX_StandardBenchmarkRuns_ModifiedById",
                table: "StandardBenchmarkRuns",
                column: "ModifiedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StandardBenchmarkRuns");
        }
    }
}
