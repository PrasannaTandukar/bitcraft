using Microsoft.AspNetCore.Identity;

namespace Bitcraft.Domain.Identity;

public class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.NewGuid();
    }
}
