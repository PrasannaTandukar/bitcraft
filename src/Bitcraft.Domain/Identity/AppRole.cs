using Microsoft.AspNetCore.Identity;

namespace Bitcraft.Domain.Identity;

public class AppRole : IdentityRole<Guid>
{
    public AppRole() : base()
    {
        Id = Guid.NewGuid();
    }

    public AppRole(string roleName) : base(roleName)
    {
        Id = Guid.NewGuid();
    }
}
