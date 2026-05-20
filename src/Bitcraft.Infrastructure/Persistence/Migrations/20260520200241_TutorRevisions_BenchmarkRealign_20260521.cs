using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TutorRevisions_BenchmarkRealign_20260521 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveRevisionId",
                table: "Tutors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EffectiveDocumentIdsJson",
                table: "TutorConfigurationSnapshots",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EffectiveKnowledgeBindingJson",
                table: "TutorConfigurationSnapshots",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TutorRevisionId",
                table: "TutorConfigurationSnapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TutorRevisionId",
                table: "BenchmarkRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TutorDocumentFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentFolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorDocumentFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorDocumentFolders_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorDocumentFolders_AspNetUsers_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorDocumentFolders_DocumentFolders_DocumentFolderId",
                        column: x => x.DocumentFolderId,
                        principalTable: "DocumentFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorDocumentFolders_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachedVia = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorDocuments_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorDocuments_AspNetUsers_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorDocuments_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorDocuments_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PromptVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    InteractionModelId = table.Column<Guid>(type: "uuid", nullable: true),
                    QueryEmbeddingModelId = table.Column<Guid>(type: "uuid", nullable: true),
                    InteractionParametersJson = table.Column<string>(type: "jsonb", nullable: true),
                    DeploymentTarget = table.Column<int>(type: "integer", nullable: false),
                    DeploymentConfigJson = table.Column<string>(type: "jsonb", nullable: true),
                    PublishedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ArchivedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivationSlug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PreviousActivationSlug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorRevisions_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorRevisions_AspNetUsers_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorRevisions_Models_InteractionModelId",
                        column: x => x.InteractionModelId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorRevisions_Models_QueryEmbeddingModelId",
                        column: x => x.QueryEmbeddingModelId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorRevisions_PromptVersions_PromptVersionId",
                        column: x => x.PromptVersionId,
                        principalTable: "PromptVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorRevisions_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TutorRevisionTools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorRevisionTools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorRevisionTools_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorRevisionTools_AspNetUsers_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorRevisionTools_ToolDefinitions_ToolDefinitionId",
                        column: x => x.ToolDefinitionId,
                        principalTable: "ToolDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorRevisionTools_TutorRevisions_TutorRevisionId",
                        column: x => x.TutorRevisionId,
                        principalTable: "TutorRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_ActiveRevisionId",
                table: "Tutors",
                column: "ActiveRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConfigurationSnapshots_TutorRevisionId",
                table: "TutorConfigurationSnapshots",
                column: "TutorRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_TutorRevisionId",
                table: "BenchmarkRuns",
                column: "TutorRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorDocumentFolders_CreatedById",
                table: "TutorDocumentFolders",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorDocumentFolders_DocumentFolderId",
                table: "TutorDocumentFolders",
                column: "DocumentFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorDocumentFolders_ModifiedById",
                table: "TutorDocumentFolders",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorDocumentFolders_TutorId_DocumentFolderId",
                table: "TutorDocumentFolders",
                columns: new[] { "TutorId", "DocumentFolderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorDocuments_CreatedById",
                table: "TutorDocuments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorDocuments_DocumentId",
                table: "TutorDocuments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorDocuments_ModifiedById",
                table: "TutorDocuments",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorDocuments_TutorId_DocumentId",
                table: "TutorDocuments",
                columns: new[] { "TutorId", "DocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorRevisions_ActivationSlug",
                table: "TutorRevisions",
                column: "ActivationSlug",
                unique: true,
                filter: "\"ActivationSlug\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TutorRevisions_CreatedById",
                table: "TutorRevisions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorRevisions_InteractionModelId",
                table: "TutorRevisions",
                column: "InteractionModelId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorRevisions_ModifiedById",
                table: "TutorRevisions",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorRevisions_PromptVersionId",
                table: "TutorRevisions",
                column: "PromptVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorRevisions_QueryEmbeddingModelId",
                table: "TutorRevisions",
                column: "QueryEmbeddingModelId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorRevisions_Status",
                table: "TutorRevisions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TutorRevisions_TutorId_VersionNumber",
                table: "TutorRevisions",
                columns: new[] { "TutorId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorRevisionTools_CreatedById",
                table: "TutorRevisionTools",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorRevisionTools_ModifiedById",
                table: "TutorRevisionTools",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorRevisionTools_ToolDefinitionId",
                table: "TutorRevisionTools",
                column: "ToolDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorRevisionTools_TutorRevisionId_ToolDefinitionId",
                table: "TutorRevisionTools",
                columns: new[] { "TutorRevisionId", "ToolDefinitionId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BenchmarkRuns_TutorRevisions_TutorRevisionId",
                table: "BenchmarkRuns",
                column: "TutorRevisionId",
                principalTable: "TutorRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TutorConfigurationSnapshots_TutorRevisions_TutorRevisionId",
                table: "TutorConfigurationSnapshots",
                column: "TutorRevisionId",
                principalTable: "TutorRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tutors_TutorRevisions_ActiveRevisionId",
                table: "Tutors",
                column: "ActiveRevisionId",
                principalTable: "TutorRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BenchmarkRuns_TutorRevisions_TutorRevisionId",
                table: "BenchmarkRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_TutorConfigurationSnapshots_TutorRevisions_TutorRevisionId",
                table: "TutorConfigurationSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_Tutors_TutorRevisions_ActiveRevisionId",
                table: "Tutors");

            migrationBuilder.DropTable(
                name: "TutorDocumentFolders");

            migrationBuilder.DropTable(
                name: "TutorDocuments");

            migrationBuilder.DropTable(
                name: "TutorRevisionTools");

            migrationBuilder.DropTable(
                name: "TutorRevisions");

            migrationBuilder.DropIndex(
                name: "IX_Tutors_ActiveRevisionId",
                table: "Tutors");

            migrationBuilder.DropIndex(
                name: "IX_TutorConfigurationSnapshots_TutorRevisionId",
                table: "TutorConfigurationSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_BenchmarkRuns_TutorRevisionId",
                table: "BenchmarkRuns");

            migrationBuilder.DropColumn(
                name: "ActiveRevisionId",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "EffectiveDocumentIdsJson",
                table: "TutorConfigurationSnapshots");

            migrationBuilder.DropColumn(
                name: "EffectiveKnowledgeBindingJson",
                table: "TutorConfigurationSnapshots");

            migrationBuilder.DropColumn(
                name: "TutorRevisionId",
                table: "TutorConfigurationSnapshots");

            migrationBuilder.DropColumn(
                name: "TutorRevisionId",
                table: "BenchmarkRuns");
        }
    }
}
