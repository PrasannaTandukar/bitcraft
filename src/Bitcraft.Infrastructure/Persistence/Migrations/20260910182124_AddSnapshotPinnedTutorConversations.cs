using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotPinnedTutorConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TutorConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorConfigurationSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AgentSessionJson = table.Column<string>(type: "jsonb", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorConversations_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorConversations_AspNetUsers_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorConversations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TutorConversations_TutorConfigurationSnapshots_TutorConfigu~",
                        column: x => x.TutorConfigurationSnapshotId,
                        principalTable: "TutorConfigurationSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorConversations_TutorRevisions_TutorRevisionId",
                        column: x => x.TutorRevisionId,
                        principalTable: "TutorRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorConversations_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorConversationMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    AttachmentIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    ProviderModelName = table.Column<string>(type: "text", nullable: true),
                    ProviderType = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorConversationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorConversationMessages_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorConversationMessages_AspNetUsers_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorConversationMessages_TutorConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "TutorConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversationMessages_ConversationId_Sequence",
                table: "TutorConversationMessages",
                columns: new[] { "ConversationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversationMessages_CreatedById",
                table: "TutorConversationMessages",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversationMessages_ModifiedById",
                table: "TutorConversationMessages",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversations_CreatedById",
                table: "TutorConversations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversations_ModifiedById",
                table: "TutorConversations",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversations_TutorConfigurationSnapshotId",
                table: "TutorConversations",
                column: "TutorConfigurationSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversations_TutorId",
                table: "TutorConversations",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversations_TutorRevisionId",
                table: "TutorConversations",
                column: "TutorRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversations_UserId_TutorId_UpdatedUtc",
                table: "TutorConversations",
                columns: new[] { "UserId", "TutorId", "UpdatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorConversationMessages");

            migrationBuilder.DropTable(
                name: "TutorConversations");
        }
    }
}
