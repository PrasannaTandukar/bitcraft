using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeTutorLongTermMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy rows have no tutor provenance. There is no safe tutor to infer.
            migrationBuilder.Sql("DELETE FROM \"TutorMemories\";");

            migrationBuilder.DropIndex(
                name: "IX_TutorMemories_Embedding",
                table: "TutorMemories");

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "TutorMemories",
                type: "vector",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(768)",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "TutorMemories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmbeddingDimensions",
                table: "TutorMemories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmbeddingModelId",
                table: "TutorMemories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TutorConfigurationSnapshotId",
                table: "TutorMemories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TutorId",
                table: "TutorMemories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TutorRevisionId",
                table: "TutorMemories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "TutorMemories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TutorId",
                table: "TutorMemories",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "TutorMemories",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorMemories_ConversationId",
                table: "TutorMemories",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorMemories_EmbeddingModelId",
                table: "TutorMemories",
                column: "EmbeddingModelId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorMemories_TutorConfigurationSnapshotId",
                table: "TutorMemories",
                column: "TutorConfigurationSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorMemories_TutorId",
                table: "TutorMemories",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorMemories_TutorRevisionId",
                table: "TutorMemories",
                column: "TutorRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorMemories_UserId_TutorId_EmbeddingModelId_EmbeddingDime~",
                table: "TutorMemories",
                columns: new[] { "UserId", "TutorId", "EmbeddingModelId", "EmbeddingDimensions" });

            migrationBuilder.AddForeignKey(
                name: "FK_TutorMemories_AspNetUsers_UserId",
                table: "TutorMemories",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TutorMemories_Models_EmbeddingModelId",
                table: "TutorMemories",
                column: "EmbeddingModelId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TutorMemories_TutorConfigurationSnapshots_TutorConfiguratio~",
                table: "TutorMemories",
                column: "TutorConfigurationSnapshotId",
                principalTable: "TutorConfigurationSnapshots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TutorMemories_TutorRevisions_TutorRevisionId",
                table: "TutorMemories",
                column: "TutorRevisionId",
                principalTable: "TutorRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TutorMemories_Tutors_TutorId",
                table: "TutorMemories",
                column: "TutorId",
                principalTable: "Tutors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // New memories may not fit the legacy fixed 768-dimensional column.
            migrationBuilder.Sql("DELETE FROM \"TutorMemories\";");

            migrationBuilder.DropForeignKey(
                name: "FK_TutorMemories_AspNetUsers_UserId",
                table: "TutorMemories");

            migrationBuilder.DropForeignKey(
                name: "FK_TutorMemories_Models_EmbeddingModelId",
                table: "TutorMemories");

            migrationBuilder.DropForeignKey(
                name: "FK_TutorMemories_TutorConfigurationSnapshots_TutorConfiguratio~",
                table: "TutorMemories");

            migrationBuilder.DropForeignKey(
                name: "FK_TutorMemories_TutorRevisions_TutorRevisionId",
                table: "TutorMemories");

            migrationBuilder.DropForeignKey(
                name: "FK_TutorMemories_Tutors_TutorId",
                table: "TutorMemories");

            migrationBuilder.DropIndex(
                name: "IX_TutorMemories_ConversationId",
                table: "TutorMemories");

            migrationBuilder.DropIndex(
                name: "IX_TutorMemories_EmbeddingModelId",
                table: "TutorMemories");

            migrationBuilder.DropIndex(
                name: "IX_TutorMemories_TutorConfigurationSnapshotId",
                table: "TutorMemories");

            migrationBuilder.DropIndex(
                name: "IX_TutorMemories_TutorId",
                table: "TutorMemories");

            migrationBuilder.DropIndex(
                name: "IX_TutorMemories_TutorRevisionId",
                table: "TutorMemories");

            migrationBuilder.DropIndex(
                name: "IX_TutorMemories_UserId_TutorId_EmbeddingModelId_EmbeddingDime~",
                table: "TutorMemories");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "TutorMemories");

            migrationBuilder.DropColumn(
                name: "EmbeddingDimensions",
                table: "TutorMemories");

            migrationBuilder.DropColumn(
                name: "EmbeddingModelId",
                table: "TutorMemories");

            migrationBuilder.DropColumn(
                name: "TutorConfigurationSnapshotId",
                table: "TutorMemories");

            migrationBuilder.DropColumn(
                name: "TutorId",
                table: "TutorMemories");

            migrationBuilder.DropColumn(
                name: "TutorRevisionId",
                table: "TutorMemories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "TutorMemories");

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "TutorMemories",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorMemories_Embedding",
                table: "TutorMemories",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }
    }
}
