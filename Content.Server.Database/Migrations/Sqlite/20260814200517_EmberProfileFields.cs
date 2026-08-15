using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class EmberProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ember: dropped rather than renamed. EF paired these with the new columns by
            // position and would have carried "Bieselite" into religion and "Spacer" into
            // homeworld - values naming prototypes which no longer exist.
            migrationBuilder.DropColumn(
                name: "nationality",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "lifepath",
                table: "profile");

            migrationBuilder.AddColumn<string>(
                name: "homeworld",
                table: "profile",
                type: "TEXT",
                nullable: false,
                defaultValue: "EmberHomeworldMars");

            migrationBuilder.AddColumn<string>(
                name: "culture",
                table: "profile",
                type: "TEXT",
                nullable: false,
                defaultValue: "EmberCultureOther");

            migrationBuilder.AddColumn<string>(
                name: "faction",
                table: "profile",
                type: "TEXT",
                nullable: false,
                defaultValue: "EmberFactionOther");

            migrationBuilder.AddColumn<string>(
                name: "religion",
                table: "profile",
                type: "TEXT",
                nullable: false,
                defaultValue: "EmberReligionUnstated");

            // Branch and rank were on the profile since the ranks work and never reached the
            // database, so a posting did not survive a reconnect. Nullable: holding no posting is
            // a legitimate answer, and it is the default one.
            migrationBuilder.AddColumn<string>(
                name: "branch",
                table: "profile",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rank",
                table: "profile",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "profile_job_title",
                columns: table => new
                {
                    profile_job_title_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<int>(type: "INTEGER", nullable: false),
                    job_name = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_job_title", x => x.profile_job_title_id);
                    table.ForeignKey(
                        name: "FK_profile_job_title_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_skill",
                columns: table => new
                {
                    profile_skill_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<int>(type: "INTEGER", nullable: false),
                    skill_name = table.Column<string>(type: "TEXT", nullable: false),
                    level = table.Column<byte>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_skill", x => x.profile_skill_id);
                    table.ForeignKey(
                        name: "FK_profile_skill_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_profile_job_title_profile_id_job_name",
                table: "profile_job_title",
                columns: new[] { "profile_id", "job_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_profile_skill_profile_id_skill_name",
                table: "profile_skill",
                columns: new[] { "profile_id", "skill_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_job_title");

            migrationBuilder.DropTable(
                name: "profile_skill");

            // Every column this migration added, once each. The loop used to run beside an
            // explicit list that already named culture and faction, so Down asked for those two
            // twice and would have failed halfway through - on the second DropColumn, with the
            // first half of the rollback already committed.
            foreach (var column in new[] { "homeworld", "culture", "faction", "religion", "branch", "rank" })
            {
                migrationBuilder.DropColumn(name: column, table: "profile");
            }

            migrationBuilder.AddColumn<string>(
                name: "nationality",
                table: "profile",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "lifepath",
                table: "profile",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
