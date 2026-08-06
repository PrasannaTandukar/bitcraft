using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResourceMembershipTables_20260521 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Create TutorMembers (D2/D3) before dropping Tutors.OwnerId so we can backfill.
            migrationBuilder.CreateTable(
                name: "TutorMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    GrantedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedById = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorMembers_AspNetUsers_GrantedById",
                        column: x => x.GrantedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorMembers_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorMembers_GrantedById",
                table: "TutorMembers",
                column: "GrantedById");

            migrationBuilder.CreateIndex(
                name: "IX_TutorMembers_TutorId_UserId",
                table: "TutorMembers",
                columns: new[] { "TutorId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorMembers_UserId",
                table: "TutorMembers",
                column: "UserId");

            // 2) D2/D3 backfill — every existing Tutor.OwnerId becomes a TutorMember(Owner)
            //    self-granted on Tutors.CreatedUtc. Idempotent guard via NOT EXISTS so re-running
            //    is safe. gen_random_uuid() is built-in in PostgreSQL 13+ (Bitcraft requires
            //    pgvector which implies PG 13+). Role=2 matches ResourceRole.Owner per
            //    ResourceRoleTests (lock contract).
            migrationBuilder.Sql(@"
                INSERT INTO ""TutorMembers"" (""Id"", ""TutorId"", ""UserId"", ""Role"", ""GrantedUtc"", ""GrantedById"")
                SELECT gen_random_uuid(),
                       t.""Id"",
                       t.""OwnerId"",
                       2,
                       COALESCE(t.""CreatedUtc"", NOW() AT TIME ZONE 'UTC'),
                       t.""OwnerId""
                FROM ""Tutors"" t
                WHERE t.""OwnerId"" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM ""TutorMembers"" m
                      WHERE m.""TutorId"" = t.""Id"" AND m.""UserId"" = t.""OwnerId""
                  );
            ");

            // 3) Now safe to drop the legacy Tutors.OwnerId column and its indexes / FK.
            migrationBuilder.DropForeignKey(
                name: "FK_Tutors_AspNetUsers_OwnerId",
                table: "Tutors");

            migrationBuilder.DropIndex(
                name: "IX_Tutors_OwnerId",
                table: "Tutors");

            migrationBuilder.DropIndex(
                name: "IX_Tutors_OwnerId_Name",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Tutors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore OwnerId nullable first so the reverse backfill can run before NOT NULL
            // is re-applied. EF's default of Guid.Empty for AddColumn would clobber real data.
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Tutors",
                type: "uuid",
                nullable: true);

            // Reverse backfill: pick the earliest-granted Owner per tutor as the restored OwnerId.
            // Lossy when a tutor had multiple Owners; documented in PR #2 summary.
            migrationBuilder.Sql(@"
                UPDATE ""Tutors"" t
                SET ""OwnerId"" = sub.""UserId""
                FROM (
                    SELECT DISTINCT ON (""TutorId"") ""TutorId"", ""UserId""
                    FROM ""TutorMembers""
                    WHERE ""Role"" = 2
                    ORDER BY ""TutorId"", ""GrantedUtc"" ASC
                ) sub
                WHERE t.""Id"" = sub.""TutorId"";
            ");

            // Fail fast if any tutor lacks an Owner member — rather than silently inserting Guid.Empty.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM ""Tutors"" WHERE ""OwnerId"" IS NULL) THEN
                        RAISE EXCEPTION 'Down() cannot restore Tutors.OwnerId for tutors with no Owner TutorMember row.';
                    END IF;
                END $$;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "Tutors",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "TutorMembers");

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_OwnerId",
                table: "Tutors",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_OwnerId_Name",
                table: "Tutors",
                columns: new[] { "OwnerId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_Tutors_AspNetUsers_OwnerId",
                table: "Tutors",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
