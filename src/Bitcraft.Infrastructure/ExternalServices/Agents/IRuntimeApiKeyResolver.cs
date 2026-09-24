using Bitcraft.Application.Common.Models;

namespace Bitcraft.Infrastructure.ExternalServices.Agents;

public interface IRuntimeApiKeyResolver
{
    Result<string?> Resolve(TutorAgentModelConfiguration model);
}
