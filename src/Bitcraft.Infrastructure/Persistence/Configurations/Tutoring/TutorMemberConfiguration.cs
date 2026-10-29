using Bitcraft.Domain.Identity;
using Bitcraft.Domain.Tutoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bitcraft.Infrastructure.Persistence.Configurations.Tutoring;

/// <summary>
/// EF mapping for <see cref="TutorMember"/>. Implements the resource-membership pattern
/// (D2/D3, plan.erm.md §3.2 / §6): one row per (TutorId, UserId), cascade with parent tutor,
/// restrict-delete on user references so audit trails are preserved.
/// </summary>
public class TutorMemberConfiguration : IEntityTypeConfiguration<TutorMember>
{
    public void Configure(EntityTypeBuilder<TutorMember> entity)
    {
        entity.ToTable("TutorMembers");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Role)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.GrantedUtc)
            .IsRequired();

        // Single membership row per (Tutor, User).
        entity.HasIndex(x => new { x.TutorId, x.UserId })
            .IsUnique();

        // Secondary index: list-by-user is a common authorization query.
        entity.HasIndex(x => x.UserId);

        // Tutor relationship is configured from TutorConfiguration (HasMany → WithOne).
        // User and GrantedBy FKs: restrict-delete to preserve audit trails.
        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.GrantedBy)
            .WithMany()
            .HasForeignKey(x => x.GrantedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
