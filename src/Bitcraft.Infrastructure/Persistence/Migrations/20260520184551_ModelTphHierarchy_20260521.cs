using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModelTphHierarchy_20260521 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BenchmarkRuns_ModelCatalogEntries_ModelCatalogEntryId",
                table: "BenchmarkRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_TutorModelConnections_ModelCatalogEntries_ModelCatalogEntry~",
                table: "TutorModelConnections");

            migrationBuilder.DropTable(
                name: "ModelCatalogEntries");

            migrationBuilder.DropColumn(
                name: "ProviderType",
                table: "TutorConfigurationSnapshots");

            migrationBuilder.RenameColumn(
                name: "ModelCatalogEntryId",
                table: "TutorModelConnections",
                newName: "InteractionModelId");

            migrationBuilder.RenameIndex(
                name: "IX_TutorModelConnections_ModelCatalogEntryId",
                table: "TutorModelConnections",
                newName: "IX_TutorModelConnections_InteractionModelId");

            migrationBuilder.RenameColumn(
                name: "ModelCatalogEntryId",
                table: "TutorConfigurationSnapshots",
                newName: "InteractionModelId");

            migrationBuilder.RenameColumn(
                name: "ModelCatalogEntryId",
                table: "BenchmarkRuns",
                newName: "InteractionModelId");

            migrationBuilder.RenameIndex(
                name: "IX_BenchmarkRuns_ModelCatalogEntryId",
                table: "BenchmarkRuns",
                newName: "IX_BenchmarkRuns_InteractionModelId");

            migrationBuilder.CreateTable(
                name: "Models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RuntimeEngineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderModelName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SettingsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModelKind = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    EmbeddingDimensions = table.Column<int>(type: "integer", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Models", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Models_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Models_AspNetUsers_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Models_RuntimeEngines_RuntimeEngineId",
                        column: x => x.RuntimeEngineId,
                        principalTable: "RuntimeEngines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Models_CreatedById",
                table: "Models",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Models_ModifiedById",
                table: "Models",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_Models_Name",
                table: "Models",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Models_RuntimeEngineId",
                table: "Models",
                column: "RuntimeEngineId");

            migrationBuilder.AddForeignKey(
                name: "FK_BenchmarkRuns_Models_InteractionModelId",
                table: "BenchmarkRuns",
                column: "InteractionModelId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TutorModelConnections_Models_InteractionModelId",
                table: "TutorModelConnections",
                column: "InteractionModelId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BenchmarkRuns_Models_InteractionModelId",
                table: "BenchmarkRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_TutorModelConnections_Models_InteractionModelId",
                table: "TutorModelConnections");

            migrationBuilder.DropTable(
                name: "Models");

            migrationBuilder.RenameColumn(
                name: "InteractionModelId",
                table: "TutorModelConnections",
                newName: "ModelCatalogEntryId");

            migrationBuilder.RenameIndex(
                name: "IX_TutorModelConnections_InteractionModelId",
                table: "TutorModelConnections",
                newName: "IX_TutorModelConnections_ModelCatalogEntryId");

            migrationBuilder.RenameColumn(
                name: "InteractionModelId",
                table: "TutorConfigurationSnapshots",
                newName: "ModelCatalogEntryId");

            migrationBuilder.RenameColumn(
                name: "InteractionModelId",
                table: "BenchmarkRuns",
                newName: "ModelCatalogEntryId");

            migrationBuilder.RenameIndex(
                name: "IX_BenchmarkRuns_InteractionModelId",
                table: "BenchmarkRuns",
                newName: "IX_BenchmarkRuns_ModelCatalogEntryId");

            migrationBuilder.AddColumn<int>(
                name: "ProviderType",
                table: "TutorConfigurationSnapshots",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModelCatalogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApiKeySecretRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModelName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    SettingsJson = table.Column<string>(type: "jsonb", nullable: true)
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
                        name: "FK_ModelCatalogEntries_AspNetUsers_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCatalogEntries_CreatedById",
                table: "ModelCatalogEntries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCatalogEntries_ModifiedById",
                table: "ModelCatalogEntries",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCatalogEntries_Name",
                table: "ModelCatalogEntries",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BenchmarkRuns_ModelCatalogEntries_ModelCatalogEntryId",
                table: "BenchmarkRuns",
                column: "ModelCatalogEntryId",
                principalTable: "ModelCatalogEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TutorModelConnections_ModelCatalogEntries_ModelCatalogEntry~",
                table: "TutorModelConnections",
                column: "ModelCatalogEntryId",
                principalTable: "ModelCatalogEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
