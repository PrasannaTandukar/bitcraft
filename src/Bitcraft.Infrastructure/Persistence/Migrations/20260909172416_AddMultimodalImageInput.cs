using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultimodalImageInput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModelCapabilities",
                table: "TutorConfigurationSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Capabilities",
                table: "Models",
                type: "integer",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ChatAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ByteLength = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatAttachments_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatAttachments_AspNetUsers_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_CreatedById_Id",
                table: "ChatAttachments",
                columns: new[] { "CreatedById", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_ExpiresUtc",
                table: "ChatAttachments",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_ModifiedById",
                table: "ChatAttachments",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_StorageKey",
                table: "ChatAttachments",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatAttachments");

            migrationBuilder.DropColumn(
                name: "ModelCapabilities",
                table: "TutorConfigurationSnapshots");

            migrationBuilder.DropColumn(
                name: "Capabilities",
                table: "Models");
        }
    }
}
