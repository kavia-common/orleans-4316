using System;
using System.Net;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans.Serialization;
using Xunit;

namespace Orleans.Core.Tests.Serialization;

/// <summary>
/// Unit tests for OrleansJsonSerializer
/// </summary>
public class OrleansJsonSerializerTests
{
    private readonly Fixture _fixture;
    private readonly OrleansJsonSerializer _serializer;

    public OrleansJsonSerializerTests()
    {
        _fixture = new Fixture();
        var options = Options.Create(new OrleansJsonSerializerOptions());
        _serializer = new OrleansJsonSerializer(options);
    }

    [Fact]
    public void Serialize_WithSimpleObject_ShouldReturnJsonString()
    {
        // Arrange
        var testData = new TestData
        {
            Id = 123,
            Name = "Test Object",
            IsActive = true
        };

        // Act
        var json = _serializer.Serialize(testData, typeof(TestData));

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("123");
        json.Should().Contain("Test Object");
    }

    [Fact]
    public void Deserialize_WithValidJson_ShouldReturnObject()
    {
        // Arrange
        var json = "{\"Id\":123,\"Name\":\"Test Object\",\"IsActive\":true}";

        // Act
        var result = _serializer.Deserialize(typeof(TestData), json) as TestData;

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(123);
        result.Name.Should().Be("Test Object");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var original = new TestData
        {
            Id = _fixture.Create<int>(),
            Name = _fixture.Create<string>(),
            IsActive = _fixture.Create<bool>()
        };

        // Act
        var json = _serializer.Serialize(original, typeof(TestData));
        var deserialized = _serializer.Deserialize(typeof(TestData), json) as TestData;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Deserialize_WithNullInput_ShouldReturnNull()
    {
        // Act
        var result = _serializer.Deserialize(typeof(TestData), null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_WithEmptyString_ShouldReturnNull()
    {
        // Act
        var result = _serializer.Deserialize(typeof(TestData), string.Empty);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_WithWhitespaceString_ShouldReturnNull()
    {
        // Act
        var result = _serializer.Deserialize(typeof(TestData), "   ");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Serialize_WithComplexObject_ShouldHandleNestedProperties()
    {
        // Arrange
        var complexData = new ComplexTestData
        {
            Id = 456,
            Nested = new TestData
            {
                Id = 789,
                Name = "Nested Object",
                IsActive = false
            },
            Values = new[] { 1, 2, 3, 4, 5 }
        };

        // Act
        var json = _serializer.Serialize(complexData, typeof(ComplexTestData));
        var deserialized = _serializer.Deserialize(typeof(ComplexTestData), json) as ComplexTestData;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEquivalentTo(complexData);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Serialize_Deserialize_WithIntegerEdgeCases_ShouldHandleCorrectly(int value)
    {
        // Arrange
        var data = new TestData { Id = value, Name = "Edge Case", IsActive = true };

        // Act
        var json = _serializer.Serialize(data, typeof(TestData));
        var deserialized = _serializer.Deserialize(typeof(TestData), json) as TestData;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Simple String")]
    [InlineData("String with special chars: !@#$%^&*()")]
    [InlineData("String with unicode: 你好世界 🌍")]
    public void Serialize_Deserialize_WithStringEdgeCases_ShouldHandleCorrectly(string? value)
    {
        // Arrange
        var data = new TestData { Id = 1, Name = value, IsActive = true };

        // Act
        var json = _serializer.Serialize(data, typeof(TestData));
        var deserialized = _serializer.Deserialize(typeof(TestData), json) as TestData;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be(value);
    }

    // Test data classes
    private class TestData
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool IsActive { get; set; }
    }

    private class ComplexTestData
    {
        public int Id { get; set; }
        public TestData? Nested { get; set; }
        public int[]? Values { get; set; }
    }
}
