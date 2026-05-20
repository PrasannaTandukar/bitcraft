using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Admin;
using Bitcraft.UnitTests.Application.Tutoring.Fakes;
using Xunit;

namespace Bitcraft.UnitTests.Application.Admin;

public class RoleManagementFeaturesTests
{
    [Fact]
    public async Task ListRolesHandler_ForAdmin_ReturnsAvailableRoles()
    {
        // Arrange
        var roleManagement = new FakeRoleManagementService();
        var handler = new RoleManagementFeatures.ListRolesHandler(
            roleManagement,
            new FakeCurrentUserContext(Guid.NewGuid(), isAdmin: true));

        // Act
        var result = await handler.Handle(new RoleManagementFeatures.ListRolesQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, roleManagement.ListCalls);
        Assert.Equal(new[] { AppRoles.Admin, AppRoles.Editor, AppRoles.Auditor }, result.Value);
    }

    [Fact]
    public async Task ListRolesHandler_ForNonAdmin_ReturnsForbidden()
    {
        // Arrange
        var roleManagement = new FakeRoleManagementService();
        var handler = new RoleManagementFeatures.ListRolesHandler(
            roleManagement,
            new FakeCurrentUserContext(Guid.NewGuid(), isAdmin: false));

        // Act
        var result = await handler.Handle(new RoleManagementFeatures.ListRolesQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("RoleManagement.Forbidden", result.Error.Code);
        Assert.Equal(0, roleManagement.ListCalls);
    }

    [Fact]
    public async Task GetUserRolesHandler_ForAdmin_ReturnsUserMembership()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleManagement = new FakeRoleManagementService();
        roleManagement.Memberships[userId] = new UserRoleMembership(userId, "editor@example.com", [AppRoles.Editor]);
        var handler = new RoleManagementFeatures.GetUserRolesHandler(
            roleManagement,
            new FakeCurrentUserContext(Guid.NewGuid(), isAdmin: true));
        var query = new RoleManagementFeatures.GetUserRolesQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, roleManagement.GetCalls);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal("editor@example.com", result.Value.Email);
        Assert.Equal(new[] { AppRoles.Editor }, result.Value.Roles);
    }

    [Fact]
    public async Task GetUserRolesHandler_ForNonAdmin_DoesNotCallRoleService()
    {
        // Arrange
        var roleManagement = new FakeRoleManagementService();
        var handler = new RoleManagementFeatures.GetUserRolesHandler(
            roleManagement,
            new FakeCurrentUserContext(Guid.NewGuid(), isAdmin: false));
        var query = new RoleManagementFeatures.GetUserRolesQuery(Guid.NewGuid());

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("RoleManagement.Forbidden", result.Error.Code);
        Assert.Equal(0, roleManagement.GetCalls);
    }

    [Fact]
    public async Task GetUserRolesHandler_WithUnknownUser_ReturnsNotFound()
    {
        // Arrange
        var roleManagement = new FakeRoleManagementService();
        var handler = new RoleManagementFeatures.GetUserRolesHandler(
            roleManagement,
            new FakeCurrentUserContext(Guid.NewGuid(), isAdmin: true));
        var query = new RoleManagementFeatures.GetUserRolesQuery(Guid.NewGuid());

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(1, roleManagement.GetCalls);
        Assert.Equal("User.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AssignUserRoleHandler_ForAdmin_AssignsRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleManagement = new FakeRoleManagementService();
        var handler = new RoleManagementFeatures.AssignUserRoleHandler(
            roleManagement,
            new FakeCurrentUserContext(Guid.NewGuid(), isAdmin: true));
        var command = new RoleManagementFeatures.AssignUserRoleCommand(userId, "editor");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, roleManagement.AssignCalls);
        Assert.Equal(new[] { AppRoles.Editor }, result.Value.Roles);
    }

    [Fact]
    public async Task AssignUserRoleHandler_WithInvalidRole_ReturnsInvalidRole()
    {
        // Arrange
        var roleManagement = new FakeRoleManagementService();
        var handler = new RoleManagementFeatures.AssignUserRoleHandler(
            roleManagement,
            new FakeCurrentUserContext(Guid.NewGuid(), isAdmin: true));
        var command = new RoleManagementFeatures.AssignUserRoleCommand(Guid.NewGuid(), "Owner");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(1, roleManagement.AssignCalls);
        Assert.Equal("Role.Invalid", result.Error.Code);
    }

    [Fact]
    public async Task AssignUserRoleHandler_WithExistingRole_ReturnsAlreadyAssigned()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleManagement = new FakeRoleManagementService();
        roleManagement.Memberships[userId] = new UserRoleMembership(userId, "editor@example.com", [AppRoles.Editor]);
        var handler = new RoleManagementFeatures.AssignUserRoleHandler(
            roleManagement,
            new FakeCurrentUserContext(Guid.NewGuid(), isAdmin: true));
        var command = new RoleManagementFeatures.AssignUserRoleCommand(userId, " editor ");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(1, roleManagement.AssignCalls);
        Assert.Equal("Role.AlreadyAssigned", result.Error.Code);
        Assert.Equal(new[] { AppRoles.Editor }, roleManagement.Memberships[userId].Roles);
    }

    [Fact]
    public async Task AssignUserRoleHandler_ForNonAdmin_DoesNotCallRoleService()
    {
        // Arrange
        var roleManagement = new FakeRoleManagementService();
        var handler = new RoleManagementFeatures.AssignUserRoleHandler(
            roleManagement,
            new FakeCurrentUserContext(Guid.NewGuid(), isAdmin: false));
        var command = new RoleManagementFeatures.AssignUserRoleCommand(Guid.NewGuid(), AppRoles.Editor);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("RoleManagement.Forbidden", result.Error.Code);
        Assert.Equal(0, roleManagement.AssignCalls);
    }

    [Fact]
    public async Task RevokeUserRoleHandler_ForAdmin_RevokesRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleManagement = new FakeRoleManagementService();
        roleManagement.Memberships[userId] = new UserRoleMembership(userId, "user@example.com", [AppRoles.Editor, AppRoles.Auditor]);
        var handler = new RoleManagementFeatures.RevokeUserRoleHandler(
            roleManagement,
            new FakeCurrentUserContext(Guid.NewGuid(), isAdmin: true));
        var command = new RoleManagementFeatures.RevokeUserRoleCommand(userId, AppRoles.Auditor);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, roleManagement.RevokeCalls);
        Assert.Equal(new[] { AppRoles.Editor }, result.Value.Roles);
    }

    [Fact]
    public async Task RevokeUserRoleHandler_ForNonAdmin_DoesNotCallRoleService()
    {
        // Arrange
        var roleManagement = new FakeRoleManagementService();
        var handler = new RoleManagementFeatures.RevokeUserRoleHandler(
            roleManagement,
            new FakeCurrentUserContext(Guid.NewGuid(), isAdmin: false));
        var command = new RoleManagementFeatures.RevokeUserRoleCommand(Guid.NewGuid(), AppRoles.Auditor);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("RoleManagement.Forbidden", result.Error.Code);
        Assert.Equal(0, roleManagement.RevokeCalls);
    }

    [Fact]
    public async Task RevokeUserRoleHandler_WithMissingRole_ReturnsNotAssigned()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleManagement = new FakeRoleManagementService();
        roleManagement.Memberships[userId] = new UserRoleMembership(userId, "viewer@example.com", [AppRoles.Auditor]);
        var handler = new RoleManagementFeatures.RevokeUserRoleHandler(
            roleManagement,
            new FakeCurrentUserContext(Guid.NewGuid(), isAdmin: true));
        var command = new RoleManagementFeatures.RevokeUserRoleCommand(userId, AppRoles.Editor);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(1, roleManagement.RevokeCalls);
        Assert.Equal("Role.NotAssigned", result.Error.Code);
        Assert.Equal(new[] { AppRoles.Auditor }, roleManagement.Memberships[userId].Roles);
    }
}

public class AppRolesTests
{
    [Theory]
    [InlineData("admin", AppRoles.Admin)]
    [InlineData(" EDITOR ", AppRoles.Editor)]
    [InlineData("Auditor", AppRoles.Auditor)]
    public void Normalize_WithSupportedRole_ReturnsCanonicalRole(string role, string expected)
    {
        // Arrange / Act
        var normalized = AppRoles.Normalize(role);

        // Assert
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Owner")]
    public void Normalize_WithUnsupportedRole_ReturnsNull(string? role)
    {
        // Arrange / Act
        var normalized = AppRoles.Normalize(role);

        // Assert
        Assert.Null(normalized);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData(" EDITOR ")]
    [InlineData("Auditor")]
    public void IsSupported_WithSupportedRole_ReturnsTrue(string role)
    {
        // Arrange / Act
        var isSupported = AppRoles.IsSupported(role);

        // Assert
        Assert.True(isSupported);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Owner")]
    public void IsSupported_WithUnsupportedRole_ReturnsFalse(string? role)
    {
        // Arrange / Act
        var isSupported = AppRoles.IsSupported(role);

        // Assert
        Assert.False(isSupported);
    }
}
