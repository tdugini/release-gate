using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ReleaseGate.Api.Persistence.Migrations;

[DbContext(typeof(ReleaseGateDbContext))]
public partial class ReleaseGateDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.4")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("ReleaseGate.Api.Domain.FeatureFlag", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("Description")
                .HasMaxLength(500)
                .HasColumnType("character varying(500)");

            b.Property<string>("Key")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("character varying(120)");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("character varying(120)");

            b.Property<Guid>("ProjectId")
                .HasColumnType("uuid");

            b.HasKey("Id");

            b.HasIndex("ProjectId", "Key")
                .IsUnique();

            b.ToTable("feature_flags");
        });

        modelBuilder.Entity("ReleaseGate.Api.Domain.FeatureFlagEnvironment", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            b.Property<bool>("Enabled")
                .HasColumnType("boolean");

            b.Property<Guid>("EnvironmentId")
                .HasColumnType("uuid");

            b.Property<Guid>("FeatureFlagId")
                .HasColumnType("uuid");

            b.Property<int>("RolloutPercentage")
                .HasColumnType("integer");

            b.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("timestamp with time zone");

            b.HasKey("Id");

            b.HasIndex("EnvironmentId");

            b.HasIndex("FeatureFlagId", "EnvironmentId")
                .IsUnique();

            b.ToTable("feature_flag_environments");
        });

        modelBuilder.Entity("ReleaseGate.Api.Domain.FlagChange", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            b.Property<Guid>("FeatureFlagEnvironmentId")
                .HasColumnType("uuid");

            b.Property<bool>("PreviousEnabled")
                .HasColumnType("boolean");

            b.Property<int>("PreviousRolloutPercentage")
                .HasColumnType("integer");

            b.Property<string>("RequestedBy")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("character varying(120)");

            b.Property<bool>("RequestedEnabled")
                .HasColumnType("boolean");

            b.Property<int>("RequestedRolloutPercentage")
                .HasColumnType("integer");

            b.Property<DateTimeOffset>("RequestedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<DateTimeOffset?>("ReviewedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("ReviewedBy")
                .HasMaxLength(120)
                .HasColumnType("character varying(120)");

            b.Property<string>("Status")
                .IsRequired()
                .HasMaxLength(24)
                .HasColumnType("character varying(24)");

            b.HasKey("Id");

            b.HasIndex("FeatureFlagEnvironmentId", "RequestedAt");

            b.ToTable("flag_changes");
        });

        modelBuilder.Entity("ReleaseGate.Api.Domain.Project", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("Description")
                .HasMaxLength(500)
                .HasColumnType("character varying(500)");

            b.Property<string>("Key")
                .IsRequired()
                .HasMaxLength(80)
                .HasColumnType("character varying(80)");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("character varying(120)");

            b.HasKey("Id");

            b.HasIndex("Key")
                .IsUnique();

            b.ToTable("projects");
        });

        modelBuilder.Entity("ReleaseGate.Api.Domain.ProjectEnvironment", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("Key")
                .IsRequired()
                .HasMaxLength(40)
                .HasColumnType("character varying(40)");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(80)
                .HasColumnType("character varying(80)");

            b.Property<Guid>("ProjectId")
                .HasColumnType("uuid");

            b.Property<int>("SortOrder")
                .HasColumnType("integer");

            b.HasKey("Id");

            b.HasIndex("ProjectId", "Key")
                .IsUnique();

            b.ToTable("environments");
        });

        modelBuilder.Entity("ReleaseGate.Api.Domain.FeatureFlag", b =>
        {
            b.HasOne("ReleaseGate.Api.Domain.Project", "Project")
                .WithMany("FeatureFlags")
                .HasForeignKey("ProjectId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Project");
        });

        modelBuilder.Entity("ReleaseGate.Api.Domain.FeatureFlagEnvironment", b =>
        {
            b.HasOne("ReleaseGate.Api.Domain.ProjectEnvironment", "Environment")
                .WithMany("FlagSettings")
                .HasForeignKey("EnvironmentId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne("ReleaseGate.Api.Domain.FeatureFlag", "FeatureFlag")
                .WithMany("Environments")
                .HasForeignKey("FeatureFlagId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Environment");
            b.Navigation("FeatureFlag");
        });

        modelBuilder.Entity("ReleaseGate.Api.Domain.FlagChange", b =>
        {
            b.HasOne("ReleaseGate.Api.Domain.FeatureFlagEnvironment", "FeatureFlagEnvironment")
                .WithMany()
                .HasForeignKey("FeatureFlagEnvironmentId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("FeatureFlagEnvironment");
        });

        modelBuilder.Entity("ReleaseGate.Api.Domain.ProjectEnvironment", b =>
        {
            b.HasOne("ReleaseGate.Api.Domain.Project", "Project")
                .WithMany("Environments")
                .HasForeignKey("ProjectId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Project");
        });

        modelBuilder.Entity("ReleaseGate.Api.Domain.FeatureFlag", b =>
        {
            b.Navigation("Environments");
        });

        modelBuilder.Entity("ReleaseGate.Api.Domain.Project", b =>
        {
            b.Navigation("Environments");
            b.Navigation("FeatureFlags");
        });

        modelBuilder.Entity("ReleaseGate.Api.Domain.ProjectEnvironment", b =>
        {
            b.Navigation("FlagSettings");
        });
#pragma warning restore 612, 618
    }
}
