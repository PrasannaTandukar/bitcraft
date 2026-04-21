using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<AuthResult> RegisterAsync(string email, string password, Guid? institutionId);
    Task<AuthResult> LoginAsync(string email, string password);
}
