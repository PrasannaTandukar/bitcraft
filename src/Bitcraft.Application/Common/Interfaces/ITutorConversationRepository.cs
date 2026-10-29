using Bitcraft.Domain.Tutoring;
namespace Bitcraft.Application.Common.Interfaces;

public interface ITutorConversationRepository
{
    Task<TutorConversation?> GetAsync(Guid id, Guid userId, Guid tutorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TutorConversation>> ListAsync(Guid userId, Guid tutorId, CancellationToken cancellationToken);
    Task AddAsync(TutorConversation conversation, CancellationToken cancellationToken);
    Task<bool> SaveAsync(TutorConversation conversation, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(TutorConversation conversation, CancellationToken cancellationToken);
}
