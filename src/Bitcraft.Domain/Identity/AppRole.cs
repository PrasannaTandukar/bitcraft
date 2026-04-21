using Microsoft.AspNetCore.Identity;

namespace Bitcraft.Domain.Identity;

public class AppRole : IdentityRole<Guid>
{
    public AppRole() : base()
    {
    }

    public AppRole(string roleName) : base(roleName)
    {
    }
}
