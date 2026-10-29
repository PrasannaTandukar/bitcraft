using Bitcraft.Domain.Tutoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Bitcraft.Infrastructure.Persistence.Configurations.Tutoring;

public sealed class TutorConversationConfiguration : IEntityTypeConfiguration<TutorConversation>
{
    public void Configure(EntityTypeBuilder<TutorConversation> builder)
    {
        builder.ToTable("TutorConversations");
        builder.ConfigureBaseModel();
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.Version).IsConcurrencyToken();
        builder.Property(value => value.Title).HasMaxLength(100);
        builder.Property(value => value.AgentSessionJson).HasColumnType("text");
        builder.HasIndex(value => new { value.UserId, value.TutorId, value.UpdatedUtc });
        builder.HasOne<Bitcraft.Domain.Identity.AppUser>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tutor>().WithMany().HasForeignKey(value => value.TutorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TutorRevision>().WithMany().HasForeignKey(value => value.TutorRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TutorConfigurationSnapshot>().WithMany().HasForeignKey(value => value.TutorConfigurationSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(value => value.Messages).WithOne().HasForeignKey(value => value.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class TutorConversationMessageConfiguration : IEntityTypeConfiguration<TutorConversationMessage>
{
    public void Configure(EntityTypeBuilder<TutorConversationMessage> builder)
    {
        builder.ToTable("TutorConversationMessages");
        builder.ConfigureBaseModel();
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.HasIndex(value => new { value.ConversationId, value.Sequence }).IsUnique();
        builder.Property(value => value.Role).HasMaxLength(16);
        builder.Property(value => value.Content).IsRequired();
        builder.OwnsOne(value => value.Provenance, provenance =>
        {
            provenance.ToTable("TutorConversationMessageProvenance");
            provenance.WithOwner().HasForeignKey("MessageId");
            provenance.Property<Guid>("MessageId");
            provenance.HasKey("MessageId");
            provenance.Property(value => value.ProviderModelName).IsRequired();
            provenance.Property(value => value.ProviderType).HasMaxLength(32).IsRequired();
            // All other identifiers are historical scalar values, intentionally without live-resource FKs.
            provenance.OwnsMany(value => value.KnowledgeEvidence, evidence =>
            {
                evidence.ToTable("TutorMessageRetrievedKnowledgeEvidence");
                evidence.WithOwner().HasForeignKey("MessageId");
                evidence.HasKey(value => value.Id);
                evidence.Property(value => value.Id).ValueGeneratedNever();
                evidence.Property(value => value.ContentExcerpt)
                    .HasMaxLength(TutorRetrievedKnowledgeEvidence.MaximumExcerptCharacters).IsRequired();
                evidence.HasIndex("MessageId", nameof(TutorRetrievedKnowledgeEvidence.RetrievalRank)).IsUnique();
            });
            provenance.Navigation(value => value.KnowledgeEvidence).HasField("knowledgeEvidence")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            provenance.OwnsMany(value => value.ToolEvidence, evidence =>
            {
                evidence.ToTable("TutorMessageToolExecutionEvidence");
                evidence.WithOwner().HasForeignKey("MessageId");
                evidence.HasKey(value => value.Id);
                evidence.Property(value => value.Id).ValueGeneratedNever();
                evidence.Property(value => value.ToolName).IsRequired();
                evidence.Property(value => value.Status).HasConversion<string>().HasMaxLength(16);
                evidence.Property(value => value.ErrorCategory).HasMaxLength(32);
            });
            provenance.Navigation(value => value.ToolEvidence).HasField("toolEvidence")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
