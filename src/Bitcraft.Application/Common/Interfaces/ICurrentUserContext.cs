namespace Bitcraft.Application.Common.Interfaces;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    Guid? UserIdOrNull { get; }
    bool IsInRole(string role);
}