using Bitcraft.Domain.Benchmarking;
using Bitcraft.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Benchmarking;

public class BenchmarkQueueStateConfiguration : IEntityTypeConfiguration<BenchmarkQueueState>
{
    public void Configure(EntityTypeBuilder<BenchmarkQueueState> entity)
    {
        entity.ToTable("BenchmarkQueueStates");

        entity.ConfigureBaseModel();

        entity.Property(x => x.UpdatedAt)
            .HasColumnType("timestamp with time zone");

        // No HasData: BaseModel.Id has a private setter, and the repository
        // seeds the singleton row on first use (GetOrCreateAsync) instead of
        // the migration carrying a fixed-key seed row.
    }
}
