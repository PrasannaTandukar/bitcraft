using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Tutoring;

public class DeploymentProfileConfiguration : IEntityTypeConfiguration<DeploymentProfile>
{
    public void Configure(EntityTypeBuilder<DeploymentProfile> entity)
    {
        entity.ToTable("DeploymentProfiles");

        entity.ConfigureBaseModel();

        entity.Property(x => x.Target)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.ConfigurationJson)
            .HasColumnType("jsonb");

        entity.HasIndex(x => x.TutorId).IsUnique();
    }
}
