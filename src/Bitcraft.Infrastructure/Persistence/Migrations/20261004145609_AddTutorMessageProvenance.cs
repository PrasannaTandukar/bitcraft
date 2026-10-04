using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorMessageProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TutorConversationMessageProvenance",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorConfigurationSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    InteractionModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderModelName = table.Column<string>(type: "text", nullable: false),
                    ProviderType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CapturedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MemoryContextProvided = table.Column<bool>(type: "boolean", nullable: false),
                    RetrievedMemoryCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorConversationMessageProvenance", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_TutorConversationMessageProvenance_TutorConversationMessage~",
                        column: x => x.MessageId,
                        principalTable: "TutorConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorMessageRetrievedKnowledgeEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentName = table.Column<string>(type: "text", nullable: true),
                    DocumentChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    ContentExcerpt = table.Column<string>(type: "character varying(1201)", maxLength: 1201, nullable: false),
                    RetrievalRank = table.Column<int>(type: "integer", nullable: false),
                    CosineSimilarity = table.Column<double>(type: "double precision", nullable: true),
                    EmbeddingModelId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmbeddingProviderModelName = table.Column<string>(type: "text", nullable: true),
                    EmbeddingDimensions = table.Column<int>(type: "integer", nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorMessageRetrievedKnowledgeEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorMessageRetrievedKnowledgeEvidence_TutorConversationMes~",
                        column: x => x.MessageId,
                        principalTable: "TutorConversationMessageProvenance",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorMessageToolExecutionEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<double>(type: "double precision", nullable: false),
                    ErrorCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorMessageToolExecutionEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorMessageToolExecutionEvidence_TutorConversationMessageP~",
                        column: x => x.MessageId,
                        principalTable: "TutorConversationMessageProvenance",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorMessageRetrievedKnowledgeEvidence_MessageId_RetrievalR~",
                table: "TutorMessageRetrievedKnowledgeEvidence",
                columns: new[] { "MessageId", "RetrievalRank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorMessageToolExecutionEvidence_MessageId",
                table: "TutorMessageToolExecutionEvidence",
                column: "MessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorMessageRetrievedKnowledgeEvidence");

            migrationBuilder.DropTable(
                name: "TutorMessageToolExecutionEvidence");

            migrationBuilder.DropTable(
                name: "TutorConversationMessageProvenance");
        }
    }
}
