using System;
using Bitcraft.Application.Common.Models;
using Xunit;

namespace Bitcraft.UnitTests.Application.Common.Models;

public class ErrorTests
{
    [Fact]
    public void Error_NullValues_ShouldHandleGracefully()
    {
        // Arrange & Act
        var error = new Error(null!, null!);

        // Assert
        Assert.Null(error.Code);
        Assert.Null(error.Description);
    }

    [Fact]
    public void Error_Equality_SameValuesAreEqual()
    {
        // Arrange
        var error1 = new Error("Auth.Failed", "Invalid credentials");
        var error2 = new Error("Auth.Failed", "Invalid credentials");

        // Act & Assert
        Assert.Equal(error1, error2);
    }
}
