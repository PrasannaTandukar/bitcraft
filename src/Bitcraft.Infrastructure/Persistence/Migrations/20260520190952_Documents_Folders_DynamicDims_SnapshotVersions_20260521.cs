using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Documents_Folders_DynamicDims_SnapshotVersions_20260521 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KnowledgeChunks_Embedding",
                table: "KnowledgeChunks");

            migrationBuilder.AddColumn<int>(
                name: "VersionNumber",
                table: "TutorConfigurationSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "KnowledgeChunks",
                type: "vector",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(768)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmbeddingDimensions",
                table: "KnowledgeChunks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmbeddingModelId",
                table: "KnowledgeChunks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentFolderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentFolders_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentFolders_AspNetUsers_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentFolders_DocumentFolders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "DocumentFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Backfill VersionNumber for any pre-existing snapshots so the new unique
            // index over (TutorId, VersionNumber) does not collide on the default value.
            migrationBuilder.Sql("""
                UPDATE "TutorConfigurationSnapshots" AS t
                SET "VersionNumber" = sub.rn
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "TutorId" ORDER BY "CapturedUtc", "Id") AS rn
                    FROM "TutorConfigurationSnapshots"
                ) AS sub
                WHERE t."Id" = sub."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TutorConfigurationSnapshots_TutorId_VersionNumber",
                table: "TutorConfigurationSnapshots",
                columns: new[] { "TutorId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeChunks_EmbeddingModelId",
                table: "KnowledgeChunks",
                column: "EmbeddingModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FolderId",
                table: "Documents",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFolders_CreatedById_ParentFolderId_Name",
                table: "DocumentFolders",
                columns: new[] { "CreatedById", "ParentFolderId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFolders_ModifiedById",
                table: "DocumentFolders",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFolders_ParentFolderId",
                table: "DocumentFolders",
                column: "ParentFolderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_DocumentFolders_FolderId",
                table: "Documents",
                column: "FolderId",
                principalTable: "DocumentFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeChunks_Models_EmbeddingModelId",
                table: "KnowledgeChunks",
                column: "EmbeddingModelId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_DocumentFolders_FolderId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeChunks_Models_EmbeddingModelId",
                table: "KnowledgeChunks");

            migrationBuilder.DropTable(
                name: "DocumentFolders");

            migrationBuilder.DropIndex(
                name: "IX_TutorConfigurationSnapshots_TutorId_VersionNumber",
                table: "TutorConfigurationSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeChunks_EmbeddingModelId",
                table: "KnowledgeChunks");

            migrationBuilder.DropIndex(
                name: "IX_Documents_FolderId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "VersionNumber",
                table: "TutorConfigurationSnapshots");

            migrationBuilder.DropColumn(
                name: "EmbeddingDimensions",
                table: "KnowledgeChunks");

            migrationBuilder.DropColumn(
                name: "EmbeddingModelId",
                table: "KnowledgeChunks");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Documents");

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "KnowledgeChunks",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeChunks_Embedding",
                table: "KnowledgeChunks",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }
    }
}
