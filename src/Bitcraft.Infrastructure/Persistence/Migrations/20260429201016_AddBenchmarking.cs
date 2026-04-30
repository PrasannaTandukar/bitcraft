using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBenchmarking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BenchmarkTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    GuidancePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MetricsJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenchmarkTemplates_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BenchmarkTemplates_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BenchmarkTemplates_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BenchmarkRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorConfigurationSnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    BenchmarkTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelCatalogEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelAlias = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConsistencyRuns = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    QueuedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalCaseCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedCaseCount = table.Column<int>(type: "integer", nullable: false),
                    ProgressPercent = table.Column<double>(type: "double precision", nullable: false),
                    ProgressStage = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ProgressMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastProgressUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EngineName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EngineVersion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    AverageScore = table.Column<double>(type: "double precision", nullable: true),
                    PassRate = table.Column<double>(type: "double precision", nullable: true),
                    TotalLatencyMs = table.Column<double>(type: "double precision", nullable: true),
                    TotalTokens = table.Column<double>(type: "double precision", nullable: true),
                    TokensPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    EstimatedCo2Grams = table.Column<double>(type: "double precision", nullable: true),
                    SummaryJson = table.Column<string>(type: "jsonb", nullable: true),
                    SystemMetricsJson = table.Column<string>(type: "jsonb", nullable: true),
                    RawResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FailureDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenchmarkRuns_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BenchmarkRuns_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BenchmarkRuns_BenchmarkTemplates_BenchmarkTemplateId",
                        column: x => x.BenchmarkTemplateId,
                        principalTable: "BenchmarkTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BenchmarkRuns_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BenchmarkRunCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BenchmarkRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseIndex = table.Column<int>(type: "integer", nullable: false),
                    InputText = table.Column<string>(type: "text", nullable: false),
                    ActualOutput = table.Column<string>(type: "text", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "text", nullable: true),
                    ContextText = table.Column<string>(type: "text", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: true),
                    Score = table.Column<double>(type: "double precision", nullable: true),
                    Reason = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    MetricsJson = table.Column<string>(type: "jsonb", nullable: true),
                    PerformanceJson = table.Column<string>(type: "jsonb", nullable: true),
                    ResourceJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkRunCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenchmarkRunCases_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BenchmarkRunCases_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BenchmarkRunCases_BenchmarkRuns_BenchmarkRunId",
                        column: x => x.BenchmarkRunId,
                        principalTable: "BenchmarkRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRunCases_BenchmarkRunId_CaseIndex",
                table: "BenchmarkRunCases",
                columns: new[] { "BenchmarkRunId", "CaseIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRunCases_CreatedById",
                table: "BenchmarkRunCases",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRunCases_EditedById",
                table: "BenchmarkRunCases",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_BenchmarkTemplateId",
                table: "BenchmarkRuns",
                column: "BenchmarkTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_CreatedById",
                table: "BenchmarkRuns",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_EditedById",
                table: "BenchmarkRuns",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_Status",
                table: "BenchmarkRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_TutorId_QueuedUtc",
                table: "BenchmarkRuns",
                columns: new[] { "TutorId", "QueuedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkTemplates_CreatedById",
                table: "BenchmarkTemplates",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkTemplates_EditedById",
                table: "BenchmarkTemplates",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkTemplates_OwnerId_Key",
                table: "BenchmarkTemplates",
                columns: new[] { "OwnerId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BenchmarkRunCases");

            migrationBuilder.DropTable(
                name: "BenchmarkRuns");

            migrationBuilder.DropTable(
                name: "BenchmarkTemplates");
        }
    }
}
