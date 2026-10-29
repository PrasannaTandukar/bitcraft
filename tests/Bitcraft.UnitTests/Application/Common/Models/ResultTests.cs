using System;
using Bitcraft.Application.Common.Models;
using Xunit;

namespace Bitcraft.UnitTests.Application.Common.Models;

public class ResultTests
{
    [Fact]
    public void Constructor_WhenIsSuccessAndErrorNone_CreatesResultSuccessfully()
    {
        // Arrange & Act
        var result = Result.Success();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Constructor_WhenIsSuccessAndHasError_ThrowsArgumentException()
    {
        // Arrange
        var error = new Error("TestError", "Test description");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new Result(true, error));
        Assert.Contains("Invalid error", exception.Message);
    }

    [Fact]
    public void Constructor_WhenIsFailureAndErrorNone_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new Result(false, Error.None));
        Assert.Contains("Invalid error", exception.Message);
    }

    [Fact]
    public void ValueProperty_OnFailureResult_ThrowsInvalidOperationException()
    {
        // Arrange
        var error = new Error("MissingValue", "The value sequence is missing.");
        var failureResult = Result.Failure<string>(error);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => failureResult.Value);
        Assert.Equal("The value of a failure result can not be accessed.", exception.Message);
    }

    [Fact]
    public void ValueProperty_OnSuccessResult_ReturnsValidValue()
    {
        // Arrange
        string expectedValue = "SecureDataPayload";
        var successResult = Result.Success(expectedValue);

        // Act
        var value = successResult.Value;

        // Assert
        Assert.Equal(expectedValue, value);
        Assert.True(successResult.IsSuccess);
    }

    [Fact]
    public void ErrorNullValue_WhenChecked_ContainsCorrectProperties()
    {
        // Arrange & Act
        var error = Error.NullValue;

        // Assert
        Assert.Equal("Error.NullValue", error.Code);
        Assert.Equal("The specified result value is null.", error.Description);
    }
}
