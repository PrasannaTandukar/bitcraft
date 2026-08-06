using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitcraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixTutorMemoryAuditColumnMerge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('"TutorMemories"') IS NOT NULL THEN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.table_constraints
                            WHERE table_name = 'TutorMemories'
                              AND constraint_name = 'FK_TutorMemories_AspNetUsers_EditedById'
                        ) THEN
                            ALTER TABLE "TutorMemories" DROP CONSTRAINT "FK_TutorMemories_AspNetUsers_EditedById";
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM pg_indexes
                            WHERE schemaname = current_schema()
                              AND tablename = 'TutorMemories'
                              AND indexname = 'IX_TutorMemories_EditedById'
                        ) THEN
                            DROP INDEX "IX_TutorMemories_EditedById";
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'TutorMemories'
                              AND column_name = 'EditedUtc'
                        ) AND NOT EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'TutorMemories'
                              AND column_name = 'ModifiedUtc'
                        ) THEN
                            ALTER TABLE "TutorMemories" RENAME COLUMN "EditedUtc" TO "ModifiedUtc";
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'TutorMemories'
                              AND column_name = 'EditedById'
                        ) AND NOT EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'TutorMemories'
                              AND column_name = 'ModifiedById'
                        ) THEN
                            ALTER TABLE "TutorMemories" RENAME COLUMN "EditedById" TO "ModifiedById";
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'TutorMemories'
                              AND column_name = 'ModifiedUtc'
                        ) THEN
                            ALTER TABLE "TutorMemories" ADD COLUMN "ModifiedUtc" timestamp with time zone NULL;
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'TutorMemories'
                              AND column_name = 'ModifiedById'
                        ) THEN
                            ALTER TABLE "TutorMemories" ADD COLUMN "ModifiedById" uuid NULL;
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.table_constraints
                            WHERE table_name = 'TutorMemories'
                              AND constraint_name = 'FK_TutorMemories_AspNetUsers_ModifiedById'
                        ) THEN
                            ALTER TABLE "TutorMemories"
                            ADD CONSTRAINT "FK_TutorMemories_AspNetUsers_ModifiedById"
                            FOREIGN KEY ("ModifiedById") REFERENCES "AspNetUsers" ("Id")
                            ON DELETE RESTRICT;
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1 FROM pg_indexes
                            WHERE schemaname = current_schema()
                              AND tablename = 'TutorMemories'
                              AND indexname = 'IX_TutorMemories_ModifiedById'
                        ) THEN
                            CREATE INDEX "IX_TutorMemories_ModifiedById"
                            ON "TutorMemories" ("ModifiedById");
                        END IF;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('"TutorMemories"') IS NOT NULL THEN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.table_constraints
                            WHERE table_name = 'TutorMemories'
                              AND constraint_name = 'FK_TutorMemories_AspNetUsers_ModifiedById'
                        ) THEN
                            ALTER TABLE "TutorMemories" DROP CONSTRAINT "FK_TutorMemories_AspNetUsers_ModifiedById";
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM pg_indexes
                            WHERE schemaname = current_schema()
                              AND tablename = 'TutorMemories'
                              AND indexname = 'IX_TutorMemories_ModifiedById'
                        ) THEN
                            DROP INDEX "IX_TutorMemories_ModifiedById";
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'TutorMemories'
                              AND column_name = 'ModifiedUtc'
                        ) AND NOT EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'TutorMemories'
                              AND column_name = 'EditedUtc'
                        ) THEN
                            ALTER TABLE "TutorMemories" RENAME COLUMN "ModifiedUtc" TO "EditedUtc";
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'TutorMemories'
                              AND column_name = 'ModifiedById'
                        ) AND NOT EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'TutorMemories'
                              AND column_name = 'EditedById'
                        ) THEN
                            ALTER TABLE "TutorMemories" RENAME COLUMN "ModifiedById" TO "EditedById";
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.table_constraints
                            WHERE table_name = 'TutorMemories'
                              AND constraint_name = 'FK_TutorMemories_AspNetUsers_EditedById'
                        ) AND EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'TutorMemories'
                              AND column_name = 'EditedById'
                        ) THEN
                            ALTER TABLE "TutorMemories"
                            ADD CONSTRAINT "FK_TutorMemories_AspNetUsers_EditedById"
                            FOREIGN KEY ("EditedById") REFERENCES "AspNetUsers" ("Id")
                            ON DELETE RESTRICT;
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1 FROM pg_indexes
                            WHERE schemaname = current_schema()
                              AND tablename = 'TutorMemories'
                              AND indexname = 'IX_TutorMemories_EditedById'
                        ) AND EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name = 'TutorMemories'
                              AND column_name = 'EditedById'
                        ) THEN
                            CREATE INDEX "IX_TutorMemories_EditedById"
                            ON "TutorMemories" ("EditedById");
                        END IF;
                    END IF;
                END $$;
                """);
        }
    }
}
