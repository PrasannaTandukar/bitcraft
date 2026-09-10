using Bitcraft.Domain.Modeling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Modeling;

public sealed class InteractionModelConfiguration : IEntityTypeConfiguration<InteractionModel>
{
    public void Configure(EntityTypeBuilder<InteractionModel> entity) =>
        entity.Property(x => x.Capabilities).HasConversion<int>().HasDefaultValue(ModelCapabilities.None);
}
