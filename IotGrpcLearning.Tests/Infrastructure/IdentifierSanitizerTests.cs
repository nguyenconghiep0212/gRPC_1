using IotGrpcLearning.Infrastructure;
using System;
using Xunit;

namespace IotGrpcLearning.Tests.Infrastructure;

public class IdentifierSanitizerTests
{
    [Theory]
    [InlineData("users")]
    [InlineData("Users")]
    [InlineData("user_table")]
    [InlineData("_private")]
    [InlineData("table123")]
    public void ValidateIdentifier_ValidNames_ReturnsName(string identifier)
    {
        // Act
        var result = IdentifierSanitizer.ValidateIdentifier(identifier);

        // Assert
        Assert.Equal(identifier, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ValidateIdentifier_NullOrWhitespace_ThrowsArgumentException(string? identifier)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => IdentifierSanitizer.ValidateIdentifier(identifier!));
    }

    [Theory]
    [InlineData("user-table")]
    [InlineData("user.table")]
    [InlineData("user table")]
    [InlineData("user;DROP TABLE")]
    [InlineData("123users")]
    [InlineData("user'table")]
    public void ValidateIdentifier_InvalidCharacters_ThrowsArgumentException(string identifier)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => IdentifierSanitizer.ValidateIdentifier(identifier));
    }

    [Theory]
    [InlineData("SELECT")]
    [InlineData("DROP")]
    [InlineData("delete")]
    [InlineData("Union")]
    public void ValidateIdentifier_ReservedKeywords_ThrowsArgumentException(string identifier)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => IdentifierSanitizer.ValidateIdentifier(identifier));
    }

    [Fact]
    public void ValidateIdentifier_TooLong_ThrowsArgumentException()
    {
        // Arrange
        var identifier = new string('a', 129);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => IdentifierSanitizer.ValidateIdentifier(identifier));
    }

    [Fact]
    public void QuoteIdentifier_ValidName_ReturnsQuoted()
    {
        // Act
        var result = IdentifierSanitizer.QuoteIdentifier("users");

        // Assert
        Assert.Equal("\"users\"", result);
    }

    [Fact]
    public void QuoteIdentifier_InvalidName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => IdentifierSanitizer.QuoteIdentifier("DROP TABLE"));
    }
}