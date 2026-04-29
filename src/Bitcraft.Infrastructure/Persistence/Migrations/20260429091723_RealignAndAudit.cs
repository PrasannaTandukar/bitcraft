using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RealignAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentChunks");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourcePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RawText = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmbeddingProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IngestedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeChunks_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeChunks_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeChunks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelCatalogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApiKeySecretRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SettingsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCatalogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelCatalogEntries_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelCatalogEntries_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Target = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentProfiles_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeploymentProfiles_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromptVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptVersions_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromptVersions_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tutors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActivePromptVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tutors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tutors_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tutors_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tutors_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tutors_PromptVersions_ActivePromptVersionId",
                        column: x => x.ActivePromptVersionId,
                        principalTable: "PromptVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ToolDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    JsonSchema = table.Column<string>(type: "jsonb", nullable: false),
                    ExecutionUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolDefinitions_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToolDefinitions_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToolDefinitions_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorConfigurationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PromptVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptContent = table.Column<string>(type: "text", nullable: true),
                    TutorModelConnectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelCatalogEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderType = table.Column<int>(type: "integer", nullable: true),
                    ModelName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModelSettingsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ToolsJson = table.Column<string>(type: "jsonb", nullable: true),
                    KnowledgeSourcesJson = table.Column<string>(type: "jsonb", nullable: true),
                    DeploymentProfileJson = table.Column<string>(type: "jsonb", nullable: true),
                    CapturedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CapturedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorConfigurationSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorConfigurationSnapshots_AspNetUsers_CapturedById",
                        column: x => x.CapturedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorConfigurationSnapshots_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorConfigurationSnapshots_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorConfigurationSnapshots_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorKnowledgeSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorKnowledgeSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorKnowledgeSources_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorKnowledgeSources_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorKnowledgeSources_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TutorKnowledgeSources_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorModelConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelCatalogEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverrideSettingsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorModelConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorModelConnections_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorModelConnections_AspNetUsers_EditedById",
                        column: x => x.EditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorModelConnections_ModelCatalogEntries_ModelCatalogEntry~",
                        column: x => x.ModelCatalogEntryId,
                        principalTable: "ModelCatalogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorModelConnections_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CreatedById",
                table: "Documents",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_EditedById",
                table: "Documents",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Name",
                table: "Documents",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentProfiles_CreatedById",
                table: "DeploymentProfiles",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentProfiles_EditedById",
                table: "DeploymentProfiles",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentProfiles_TutorId",
                table: "DeploymentProfiles",
                column: "TutorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeChunks_CreatedById",
                table: "KnowledgeChunks",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeChunks_DocumentId_ChunkIndex",
                table: "KnowledgeChunks",
                columns: new[] { "DocumentId", "ChunkIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeChunks_Embedding",
                table: "KnowledgeChunks",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeChunks_EditedById",
                table: "KnowledgeChunks",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCatalogEntries_CreatedById",
                table: "ModelCatalogEntries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCatalogEntries_EditedById",
                table: "ModelCatalogEntries",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCatalogEntries_Name",
                table: "ModelCatalogEntries",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromptVersions_CreatedById",
                table: "PromptVersions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVersions_EditedById",
                table: "PromptVersions",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVersions_TutorId_VersionNumber",
                table: "PromptVersions",
                columns: new[] { "TutorId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_CreatedById",
                table: "ToolDefinitions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_EditedById",
                table: "ToolDefinitions",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_TutorId_Name",
                table: "ToolDefinitions",
                columns: new[] { "TutorId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorConfigurationSnapshots_CapturedById",
                table: "TutorConfigurationSnapshots",
                column: "CapturedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConfigurationSnapshots_CapturedUtc",
                table: "TutorConfigurationSnapshots",
                column: "CapturedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConfigurationSnapshots_CreatedById",
                table: "TutorConfigurationSnapshots",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConfigurationSnapshots_EditedById",
                table: "TutorConfigurationSnapshots",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConfigurationSnapshots_TutorId",
                table: "TutorConfigurationSnapshots",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorKnowledgeSources_CreatedById",
                table: "TutorKnowledgeSources",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorKnowledgeSources_DocumentId",
                table: "TutorKnowledgeSources",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorKnowledgeSources_EditedById",
                table: "TutorKnowledgeSources",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorKnowledgeSources_TutorId_DocumentId",
                table: "TutorKnowledgeSources",
                columns: new[] { "TutorId", "DocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorModelConnections_CreatedById",
                table: "TutorModelConnections",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorModelConnections_EditedById",
                table: "TutorModelConnections",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorModelConnections_ModelCatalogEntryId",
                table: "TutorModelConnections",
                column: "ModelCatalogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorModelConnections_TutorId",
                table: "TutorModelConnections",
                column: "TutorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_ActivePromptVersionId",
                table: "Tutors",
                column: "ActivePromptVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_CreatedById",
                table: "Tutors",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_EditedById",
                table: "Tutors",
                column: "EditedById");

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_OwnerId",
                table: "Tutors",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_OwnerId_Name",
                table: "Tutors",
                columns: new[] { "OwnerId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentProfiles_Tutors_TutorId",
                table: "DeploymentProfiles",
                column: "TutorId",
                principalTable: "Tutors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PromptVersions_Tutors_TutorId",
                table: "PromptVersions",
                column: "TutorId",
                principalTable: "Tutors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PromptVersions_Tutors_TutorId",
                table: "PromptVersions");

            migrationBuilder.DropTable(
                name: "DeploymentProfiles");

            migrationBuilder.DropTable(
                name: "KnowledgeChunks");

            migrationBuilder.DropTable(
                name: "ToolDefinitions");

            migrationBuilder.DropTable(
                name: "TutorConfigurationSnapshots");

            migrationBuilder.DropTable(
                name: "TutorKnowledgeSources");

            migrationBuilder.DropTable(
                name: "TutorModelConnections");

            migrationBuilder.DropTable(
                name: "ModelCatalogEntries");

            migrationBuilder.DropTable(
                name: "Tutors");

            migrationBuilder.DropTable(
                name: "PromptVersions");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourcePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Name",
                table: "Documents",
                column: "Name");

            migrationBuilder.CreateTable(
                name: "DocumentChunks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(768)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentChunks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId_ChunkIndex",
                table: "DocumentChunks",
                columns: new[] { "DocumentId", "ChunkIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_Embedding",
                table: "DocumentChunks",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }
    }
}
