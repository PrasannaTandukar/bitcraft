using System;
using Bitcraft.Domain.Identity;
using Xunit;

namespace Bitcraft.UnitTests.Domain.Identity;

public class AppUserTests
{
    [Fact]
    public void AppUser_Initialization_CreatesValidId()
    {
        // Arrange & Act
        var user = new AppUser();

        // Assert
        Assert.NotEqual(Guid.Empty, user.Id);
    }
}

public class AppRoleTests
{
    [Fact]
    public void AppRole_Initialization_CreatesValidId()
    {
        // Arrange & Act
        var role = new AppRole();

        // Assert
        Assert.NotEqual(Guid.Empty, role.Id);
    }

    [Fact]
    public void AppRole_InitializationWithName_SetsNameProperty()
    {
        // Arrange
        var roleName = "Admin";

        // Act
        var role = new AppRole(roleName);

        // Assert
        Assert.Equal(roleName, role.Name);
        Assert.NotEqual(Guid.Empty, role.Id);
    }
}
