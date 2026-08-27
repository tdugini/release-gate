using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ReleaseGate.Api.Persistence.Migrations;

[DbContext(typeof(ReleaseGateDbContext))]
[Migration(DatabaseMigrationBootstrapper.InitialMigrationId)]
public partial class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "projects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_projects", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "environments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_environments", x => x.Id);
                table.ForeignKey(
                    name: "FK_environments_projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "feature_flags",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_feature_flags", x => x.Id);
                table.ForeignKey(
                    name: "FK_feature_flags_projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "feature_flag_environments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FeatureFlagId = table.Column<Guid>(type: "uuid", nullable: false),
                EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                RolloutPercentage = table.Column<int>(type: "integer", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_feature_flag_environments", x => x.Id);
                table.ForeignKey(
                    name: "FK_feature_flag_environments_environments_EnvironmentId",
                    column: x => x.EnvironmentId,
                    principalTable: "environments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_feature_flag_environments_feature_flags_FeatureFlagId",
                    column: x => x.FeatureFlagId,
                    principalTable: "feature_flags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "flag_changes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FeatureFlagEnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                PreviousEnabled = table.Column<bool>(type: "boolean", nullable: false),
                PreviousRolloutPercentage = table.Column<int>(type: "integer", nullable: false),
                RequestedEnabled = table.Column<bool>(type: "boolean", nullable: false),
                RequestedRolloutPercentage = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                RequestedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReviewedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_flag_changes", x => x.Id);
                table.ForeignKey(
                    name: "FK_flag_changes_feature_flag_environments_FeatureFlagEnvironmen~",
                    column: x => x.FeatureFlagEnvironmentId,
                    principalTable: "feature_flag_environments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_environments_ProjectId_Key",
            table: "environments",
            columns: new[] { "ProjectId", "Key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_feature_flag_environments_EnvironmentId",
            table: "feature_flag_environments",
            column: "EnvironmentId");

        migrationBuilder.CreateIndex(
            name: "IX_feature_flag_environments_FeatureFlagId_EnvironmentId",
            table: "feature_flag_environments",
            columns: new[] { "FeatureFlagId", "EnvironmentId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_feature_flags_ProjectId_Key",
            table: "feature_flags",
            columns: new[] { "ProjectId", "Key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_flag_changes_FeatureFlagEnvironmentId_RequestedAt",
            table: "flag_changes",
            columns: new[] { "FeatureFlagEnvironmentId", "RequestedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_projects_Key",
            table: "projects",
            column: "Key",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "flag_changes");
        migrationBuilder.DropTable(name: "feature_flag_environments");
        migrationBuilder.DropTable(name: "environments");
        migrationBuilder.DropTable(name: "feature_flags");
        migrationBuilder.DropTable(name: "projects");
    }
}
