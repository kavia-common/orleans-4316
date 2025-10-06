using System;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Runtime;
using Xunit;

namespace Orleans.Core.Tests.Configuration;

/// <summary>
/// Unit tests for ClusterOptionsValidator
/// </summary>
public class ClusterOptionsValidatorTests
{
    private readonly Fixture _fixture;

    public ClusterOptionsValidatorTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void ValidateConfiguration_WithValidOptions_ShouldNotThrow()
    {
        // Arrange
        var options = new ClusterOptions
        {
            ClusterId = "test-cluster",
            ServiceId = "test-service"
        };
        var validator = CreateValidator(options);

        // Act
        Action act = () => validator.ValidateConfiguration();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateConfiguration_WithDefaultValues_ShouldNotThrow()
    {
        // Arrange
        var options = new ClusterOptions(); // Uses default values
        var validator = CreateValidator(options);

        // Act
        Action act = () => validator.ValidateConfiguration();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateConfiguration_WithNullClusterId_ShouldThrowOrleansConfigurationException()
    {
        // Arrange
        var options = new ClusterOptions
        {
            ClusterId = null!,
            ServiceId = "test-service"
        };
        var validator = CreateValidator(options);

        // Act
        Action act = () => validator.ValidateConfiguration();

        // Assert
        act.Should().Throw<OrleansConfigurationException>()
            .WithMessage("*ClusterId*");
    }

    [Fact]
    public void ValidateConfiguration_WithEmptyClusterId_ShouldThrowOrleansConfigurationException()
    {
        // Arrange
        var options = new ClusterOptions
        {
            ClusterId = string.Empty,
            ServiceId = "test-service"
        };
        var validator = CreateValidator(options);

        // Act
        Action act = () => validator.ValidateConfiguration();

        // Assert
        act.Should().Throw<OrleansConfigurationException>()
            .WithMessage("*ClusterId*");
    }

    [Fact]
    public void ValidateConfiguration_WithWhitespaceClusterId_ShouldThrowOrleansConfigurationException()
    {
        // Arrange
        var options = new ClusterOptions
        {
            ClusterId = "   ",
            ServiceId = "test-service"
        };
        var validator = CreateValidator(options);

        // Act
        Action act = () => validator.ValidateConfiguration();

        // Assert
        act.Should().Throw<OrleansConfigurationException>()
            .WithMessage("*ClusterId*");
    }

    [Fact]
    public void ValidateConfiguration_WithNullServiceId_ShouldThrowOrleansConfigurationException()
    {
        // Arrange
        var options = new ClusterOptions
        {
            ClusterId = "test-cluster",
            ServiceId = null!
        };
        var validator = CreateValidator(options);

        // Act
        Action act = () => validator.ValidateConfiguration();

        // Assert
        act.Should().Throw<OrleansConfigurationException>()
            .WithMessage("*ServiceId*");
    }

    [Fact]
    public void ValidateConfiguration_WithEmptyServiceId_ShouldThrowOrleansConfigurationException()
    {
        // Arrange
        var options = new ClusterOptions
        {
            ClusterId = "test-cluster",
            ServiceId = string.Empty
        };
        var validator = CreateValidator(options);

        // Act
        Action act = () => validator.ValidateConfiguration();

        // Assert
        act.Should().Throw<OrleansConfigurationException>()
            .WithMessage("*ServiceId*");
    }

    [Fact]
    public void ValidateConfiguration_WithWhitespaceServiceId_ShouldThrowOrleansConfigurationException()
    {
        // Arrange
        var options = new ClusterOptions
        {
            ClusterId = "test-cluster",
            ServiceId = "   "
        };
        var validator = CreateValidator(options);

        // Act
        Action act = () => validator.ValidateConfiguration();

        // Assert
        act.Should().Throw<OrleansConfigurationException>()
            .WithMessage("*ServiceId*");
    }

    [Fact]
    public void ValidateConfiguration_WithBothIdsInvalid_ShouldThrowForClusterId()
    {
        // Arrange - ClusterId is validated first
        var options = new ClusterOptions
        {
            ClusterId = string.Empty,
            ServiceId = string.Empty
        };
        var validator = CreateValidator(options);

        // Act
        Action act = () => validator.ValidateConfiguration();

        // Assert
        act.Should().Throw<OrleansConfigurationException>()
            .WithMessage("*ClusterId*");
    }

    [Theory]
    [InlineData("cluster-1", "service-1")]
    [InlineData("ClusterWithNumbers123", "ServiceWithNumbers456")]
    [InlineData("cluster_with_underscores", "service_with_underscores")]
    [InlineData("cluster-with-dashes", "service-with-dashes")]
    public void ValidateConfiguration_WithVariousValidFormats_ShouldNotThrow(string clusterId, string serviceId)
    {
        // Arrange
        var options = new ClusterOptions
        {
            ClusterId = clusterId,
            ServiceId = serviceId
        };
        var validator = CreateValidator(options);

        // Act
        Action act = () => validator.ValidateConfiguration();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ClusterOptions_DefaultClusterId_ShouldBeDefault()
    {
        // Arrange & Act
        var options = new ClusterOptions();

        // Assert
        options.ClusterId.Should().Be(ClusterOptions.DefaultClusterId);
    }

    [Fact]
    public void ClusterOptions_DefaultServiceId_ShouldBeDefault()
    {
        // Arrange & Act
        var options = new ClusterOptions();

        // Assert
        options.ServiceId.Should().Be(ClusterOptions.DefaultServiceId);
    }

    private ClusterOptionsValidator CreateValidator(ClusterOptions options)
    {
        var optionsWrapper = Options.Create(options);
        return new ClusterOptionsValidator(optionsWrapper);
    }
}
