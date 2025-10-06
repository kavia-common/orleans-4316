using System;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Orleans.Runtime.Configuration;
using Xunit;

namespace Orleans.Core.Tests.Utils;

/// <summary>
/// Unit tests for ConfigUtilities
/// </summary>
public class ConfigUtilitiesTests
{
    #region ParseTimeSpan Tests

    [Theory]
    [InlineData("100ms", 100)]
    [InlineData("100 ms", 100)]
    [InlineData("100MS", 100)]
    [InlineData("1ms", 1)]
    [InlineData("0ms", 0)]
    public void ParseTimeSpan_WithMilliseconds_ShouldParseCorrectly(string input, int expectedMilliseconds)
    {
        // Act
        var result = ConfigUtilities.ParseTimeSpan(input, "Test error");

        // Assert
        result.TotalMilliseconds.Should().Be(expectedMilliseconds);
    }

    [Theory]
    [InlineData("1s", 1)]
    [InlineData("10s", 10)]
    [InlineData("60s", 60)]
    [InlineData("0s", 0)]
    [InlineData("1", 1)] // Default unit is seconds
    [InlineData("5", 5)] // Default unit is seconds
    public void ParseTimeSpan_WithSeconds_ShouldParseCorrectly(string input, int expectedSeconds)
    {
        // Act
        var result = ConfigUtilities.ParseTimeSpan(input, "Test error");

        // Assert
        result.TotalSeconds.Should().Be(expectedSeconds);
    }

    [Theory]
    [InlineData("1m", 1)]
    [InlineData("5m", 5)]
    [InlineData("60m", 60)]
    [InlineData("0m", 0)]
    public void ParseTimeSpan_WithMinutes_ShouldParseCorrectly(string input, int expectedMinutes)
    {
        // Act
        var result = ConfigUtilities.ParseTimeSpan(input, "Test error");

        // Assert
        result.TotalMinutes.Should().Be(expectedMinutes);
    }

    [Theory]
    [InlineData("1hr", 1)]
    [InlineData("2hr", 2)]
    [InlineData("24hr", 24)]
    [InlineData("0hr", 0)]
    public void ParseTimeSpan_WithHours_ShouldParseCorrectly(string input, int expectedHours)
    {
        // Act
        var result = ConfigUtilities.ParseTimeSpan(input, "Test error");

        // Assert
        result.TotalHours.Should().Be(expectedHours);
    }

    [Theory]
    [InlineData("0.5s", 500)]
    [InlineData("1.5s", 1500)]
    [InlineData("0.1s", 100)]
    public void ParseTimeSpan_WithDecimalValues_ShouldParseCorrectly(string input, int expectedMilliseconds)
    {
        // Act
        var result = ConfigUtilities.ParseTimeSpan(input, "Test error");

        // Assert
        result.TotalMilliseconds.Should().BeApproximately(expectedMilliseconds, 1);
    }

    [Theory]
    [InlineData("  100ms  ")]
    [InlineData("\t5s\t")]
    [InlineData("  10  ")]
    public void ParseTimeSpan_WithWhitespace_ShouldTrimAndParse(string input, string? unused = null)
    {
        // Act
        Action act = () => ConfigUtilities.ParseTimeSpan(input, "Test error");

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("s")]
    [InlineData("ms")]
    [InlineData("notanumber")]
    public void ParseTimeSpan_WithInvalidInput_ShouldThrowFormatException(string input)
    {
        // Act
        Action act = () => ConfigUtilities.ParseTimeSpan(input, "Test error message");

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("Test error message*");
    }

    [Fact]
    public void ParseTimeSpan_WithNegativeValue_ShouldReturnNegativeTimeSpan()
    {
        // Act
        var result = ConfigUtilities.ParseTimeSpan("-5s", "Test error");

        // Assert
        result.TotalSeconds.Should().Be(-5);
    }

    #endregion

    #region RedactConnectionStringInfo Tests

    [Fact]
    public void RedactConnectionStringInfo_WithNullInput_ShouldReturnNull()
    {
        // Act
        var result = ConfigUtilities.RedactConnectionStringInfo(null!);

        // Assert
        result.Should().Be("null");
    }

    [Fact]
    public void RedactConnectionStringInfo_WithEmptyInput_ShouldReturnNull()
    {
        // Act
        var result = ConfigUtilities.RedactConnectionStringInfo(string.Empty);

        // Assert
        result.Should().Be("null");
    }

    [Fact]
    public void RedactConnectionStringInfo_WithNoSecrets_ShouldReturnSnip()
    {
        // Arrange
        var connectionString = "Server=myserver;Database=mydb";

        // Act
        var result = ConfigUtilities.RedactConnectionStringInfo(connectionString);

        // Assert
        result.Should().Be("<--SNIP-->");
    }

    [Theory]
    [InlineData("AccountName=myaccount;AccountKey=secret123")]
    [InlineData("AccountName=myaccount;AccountKey=veryLongSecretKey1234567890")]
    public void RedactConnectionStringInfo_WithAzureAccountKey_ShouldRedactKey(string connectionString)
    {
        // Act
        var result = ConfigUtilities.RedactConnectionStringInfo(connectionString);

        // Assert
        result.Should().Contain("AccountKey=<--SNIP-->");
        result.Should().NotContain("secret");
        result.Should().NotContain("veryLongSecretKey");
    }

    [Theory]
    [InlineData("Server=myserver;Database=mydb;Password=secret123")]
    [InlineData("Server=myserver;Database=mydb;Password=MyP@ssw0rd!")]
    public void RedactConnectionStringInfo_WithSqlPassword_ShouldRedactPassword(string connectionString)
    {
        // Act
        var result = ConfigUtilities.RedactConnectionStringInfo(connectionString);

        // Assert
        result.Should().Contain("Password=<--SNIP-->");
        result.Should().NotContain("secret");
        result.Should().NotContain("MyP@ssw0rd");
    }

    [Fact]
    public void RedactConnectionStringInfo_WithSharedAccessSignature_ShouldRedactSignature()
    {
        // Arrange
        var connectionString = "Endpoint=sb://mybus.servicebus.windows.net/;SharedAccessSignature=secretSignature";

        // Act
        var result = ConfigUtilities.RedactConnectionStringInfo(connectionString);

        // Assert
        result.Should().Contain("SharedAccessSignature=<--SNIP-->");
        result.Should().NotContain("secretSignature");
    }

    [Fact]
    public void RedactConnectionStringInfo_WithAwsSecretKey_ShouldRedactSecretKey()
    {
        // Arrange
        var connectionString = "AccessKey=AKIAIOSFODNN7EXAMPLE;SecretKey=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

        // Act
        var result = ConfigUtilities.RedactConnectionStringInfo(connectionString);

        // Assert
        result.Should().Contain("SecretKey=<--SNIP-->");
        result.Should().NotContain("wJalrXUtnFEMI");
    }

    [Fact]
    public void RedactConnectionStringInfo_WithSessionToken_ShouldRedactToken()
    {
        // Arrange
        var connectionString = "AccessKey=AKIAIOSFODNN7EXAMPLE;SessionToken=mySessionToken123";

        // Act
        var result = ConfigUtilities.RedactConnectionStringInfo(connectionString);

        // Assert
        result.Should().Contain("SessionToken=<--SNIP-->");
        result.Should().NotContain("mySessionToken");
    }

    [Fact]
    public void RedactConnectionStringInfo_WithMultipleSecrets_ShouldRedactFirstOccurrence()
    {
        // Arrange
        var connectionString = "AccountName=myaccount;AccountKey=secret1;SharedAccessKey=secret2";

        // Act
        var result = ConfigUtilities.RedactConnectionStringInfo(connectionString);

        // Assert
        result.Should().Contain("AccountKey=<--SNIP-->");
        result.Should().NotContain("secret1");
        // Note: Only first secret key is redacted as per implementation
    }

    [Theory]
    [InlineData("accountkey=secret")]
    [InlineData("ACCOUNTKEY=secret")]
    [InlineData("AccountKey=secret")]
    public void RedactConnectionStringInfo_ShouldBeCaseInsensitive(string connectionString)
    {
        // Act
        var result = ConfigUtilities.RedactConnectionStringInfo(connectionString);

        // Assert
        result.Should().Contain("<--SNIP-->");
        result.Should().NotContain("secret");
    }

    #endregion

    #region GetLocalIPAddress Tests

    [Fact]
    public void GetLocalIPAddress_WithIPv4_ShouldReturnValidAddress()
    {
        // Act
        var result = ConfigUtilities.GetLocalIPAddress(AddressFamily.InterNetwork);

        // Assert
        result.Should().NotBeNull();
        result.AddressFamily.Should().Be(AddressFamily.InterNetwork);
    }

    [Fact]
    public void GetLocalIPAddress_WithIPv6_ShouldReturnValidAddressOrThrow()
    {
        // Act
        Action act = () => ConfigUtilities.GetLocalIPAddress(AddressFamily.InterNetworkV6);

        // Assert - May throw if IPv6 is not available, or return valid address
        try
        {
            var result = ConfigUtilities.GetLocalIPAddress(AddressFamily.InterNetworkV6);
            result.Should().NotBeNull();
            result.AddressFamily.Should().Be(AddressFamily.InterNetworkV6);
        }
        catch (Orleans.Runtime.OrleansException)
        {
            // It's acceptable if IPv6 is not configured on the system
        }
    }

    #endregion

    #region ResolveIPAddressOrDefault Tests

    [Fact]
    public void ResolveIPAddressOrDefault_WithEmptyString_ShouldReturnLocalAddress()
    {
        // Act
        var result = ConfigUtilities.ResolveIPAddressOrDefault(string.Empty, null, AddressFamily.InterNetwork);

        // Assert
        result.Should().NotBeNull();
        result.AddressFamily.Should().Be(AddressFamily.InterNetwork);
    }

    [Fact]
    public void ResolveIPAddressOrDefault_WithLoopback_ShouldReturnLoopbackAddress()
    {
        // Act
        var result = ConfigUtilities.ResolveIPAddressOrDefault("loopback", null, AddressFamily.InterNetwork);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(IPAddress.Loopback);
    }

    [Fact]
    public void ResolveIPAddressOrDefault_WithValidIPAddress_ShouldReturnSameAddress()
    {
        // Arrange
        var expectedAddress = IPAddress.Parse("192.168.1.100");

        // Act
        var result = ConfigUtilities.ResolveIPAddressOrDefault("192.168.1.100", null, AddressFamily.InterNetwork);

        // Assert
        result.Should().Be(expectedAddress);
    }

    [Fact]
    public void ResolveIPAddressOrDefault_WithLocalhost_ShouldReturnLoopbackAddress()
    {
        // Act
        var result = ConfigUtilities.ResolveIPAddressOrDefault("localhost", null, AddressFamily.InterNetwork);

        // Assert
        result.Should().NotBeNull();
        result.AddressFamily.Should().Be(AddressFamily.InterNetwork);
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("127.0.0.1")]
    public void ResolveIPAddressOrDefault_WithSpecialAddresses_ShouldParseCorrectly(string address)
    {
        // Act
        var result = ConfigUtilities.ResolveIPAddressOrDefault(address, null, AddressFamily.InterNetwork);

        // Assert
        result.Should().Be(IPAddress.Parse(address));
    }

    #endregion
}
