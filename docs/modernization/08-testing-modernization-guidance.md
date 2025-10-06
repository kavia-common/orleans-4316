# Testing Modernization Guidance for Orleans Applications

## Overview

This document provides comprehensive guidance on modernizing testing practices for Orleans applications, building upon the testing strategy outlined in document 05-testing-strategy.md. It introduces modern testing patterns, advanced tooling, and best practices that align with .NET 9 and contemporary software engineering standards.

Modern testing practices go beyond traditional unit and integration tests to include property-based testing, snapshot testing, mutation testing, chaos engineering, and comprehensive performance validation. This guidance demonstrates how to apply these advanced techniques to Orleans applications while maintaining fast feedback loops and high confidence in system behavior.

## Modern Testing Patterns and Tools

### Property-Based Testing with CsCheck

Property-based testing generates random test inputs to verify that properties (invariants) hold true across a wide range of scenarios. This is particularly valuable for Orleans applications where grains must maintain consistency under various conditions.

#### Why Property-Based Testing for Orleans?

Traditional example-based testing validates specific scenarios, but Orleans grains handle concurrent requests, state transitions, and failure scenarios that are difficult to enumerate manually. Property-based testing automatically explores the input space to discover edge cases that might otherwise be missed.

**Benefits**:
- Discovers edge cases that manual testing misses
- Validates invariants across thousands of random inputs
- Reduces test maintenance by expressing properties rather than examples
- Excellent for testing state machines and concurrent behavior

#### Basic Property Tests

```csharp
// MyApp.Grains.Tests/PropertyTests/OrderGrainPropertyTests.cs
using CsCheck;
using Xunit;
using Orleans.TestingHost;

namespace MyApp.Grains.Tests.PropertyTests
{
    [Collection("TestCluster")]
    public class OrderGrainPropertyTests
    {
        private readonly TestCluster _cluster;
        
        public OrderGrainPropertyTests(TestClusterFixture fixture)
        {
            _cluster = fixture.Cluster;
        }
        
        [Fact]
        public void OrderGrain_TotalAmount_ShouldNeverBeNegative()
        {
            // Property: No matter what operations are performed,
            // the order total should never become negative
            
            Gen.Select(
                Gen.String[1, 50], // customerId
                Gen.String[1, 50], // productId
                Gen.Int[1, 100],   // quantity
                Gen.Double[0.01, 10000.0] // amount
            ).Sample(async (customerId, productId, quantity, amount) =>
            {
                var orderId = Guid.NewGuid();
                var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
                
                try
                {
                    await grain.PlaceOrderAsync(new PlaceOrderRequest
                    {
                        CustomerId = customerId,
                        ProductId = productId,
                        Quantity = quantity,
                        TotalAmount = (decimal)amount
                    });
                    
                    var state = await grain.GetOrderStatusAsync();
                    
                    // Assert property: Total amount should never be negative
                    Assert.True(state.TotalAmount >= 0, 
                        $"Order total became negative: {state.TotalAmount}");
                }
                catch (DomainException)
                {
                    // Domain exceptions are expected for invalid inputs
                    // Property still holds - order wasn't created with negative amount
                }
            });
        }
        
        [Fact]
        public void OrderGrain_StateTransitions_ShouldBeValid()
        {
            // Property: All state transitions should follow valid paths
            // Valid transitions: Placed -> Confirmed -> Shipped -> Delivered
            //                   Placed -> Cancelled
            //                   Confirmed -> Cancelled (if not shipped)
            
            var validTransitions = new Dictionary<string, HashSet<string>>
            {
                [""] = new() { "Placed" },
                ["Placed"] = new() { "Confirmed", "Cancelled" },
                ["Confirmed"] = new() { "Shipped", "Cancelled" },
                ["Shipped"] = new() { "Delivered" },
                ["Delivered"] = new(),
                ["Cancelled"] = new()
            };
            
            // Generate random sequences of operations
            Gen.SelectMany(
                Gen.Int[5, 20], // sequence length
                length => Gen.Array[length](Gen.Select(
                    Gen.OneOf<string>("Confirm", "Ship", "Deliver", "Cancel")
                ))
            ).Sample(async operations =>
            {
                var orderId = Guid.NewGuid();
                var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
                
                // Place initial order
                await grain.PlaceOrderAsync(new PlaceOrderRequest
                {
                    CustomerId = "CUST-TEST",
                    ProductId = "PROD-TEST",
                    Quantity = 1,
                    TotalAmount = 100.00m
                });
                
                var previousStatus = "Placed";
                
                foreach (var operation in operations)
                {
                    try
                    {
                        switch (operation)
                        {
                            case "Confirm":
                                await grain.ConfirmOrderAsync();
                                break;
                            case "Ship":
                                await grain.ShipOrderAsync();
                                break;
                            case "Deliver":
                                await grain.DeliverOrderAsync();
                                break;
                            case "Cancel":
                                await grain.CancelOrderAsync();
                                break;
                        }
                        
                        var state = await grain.GetOrderStatusAsync();
                        var newStatus = state.Status;
                        
                        // Assert: Transition should be valid
                        Assert.True(
                            validTransitions[previousStatus].Contains(newStatus),
                            $"Invalid transition from {previousStatus} to {newStatus}");
                        
                        previousStatus = newStatus;
                    }
                    catch (DomainException)
                    {
                        // Invalid operations are expected and property still holds
                        // The grain correctly rejected an invalid transition
                    }
                }
            });
        }
        
        [Fact]
        public void OrderGrain_Idempotency_ShouldHandleDuplicateRequests()
        {
            // Property: Placing the same order twice should be idempotent
            // (Orleans guarantees single activation, but clients might retry)
            
            Gen.Select(
                Gen.String[1, 50], // customerId
                Gen.String[1, 50], // productId
                Gen.Int[1, 10],    // quantity
                Gen.Double[1.0, 1000.0] // amount
            ).Sample(async (customerId, productId, quantity, amount) =>
            {
                var orderId = Guid.NewGuid();
                var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
                
                var request = new PlaceOrderRequest
                {
                    CustomerId = customerId,
                    ProductId = productId,
                    Quantity = quantity,
                    TotalAmount = (decimal)amount,
                    IdempotencyKey = Guid.NewGuid().ToString()
                };
                
                // Place order twice with same idempotency key
                var response1 = await grain.PlaceOrderAsync(request);
                var response2 = await grain.PlaceOrderAsync(request);
                
                // Assert: Both responses should be identical
                Assert.Equal(response1.OrderId, response2.OrderId);
                Assert.Equal(response1.Success, response2.Success);
                
                // Assert: Only one order should exist
                var state = await grain.GetOrderStatusAsync();
                Assert.Single(await GetOrdersByIdempotencyKey(request.IdempotencyKey));
            });
        }
    }
}
```

#### Concurrent Property Tests

```csharp
// MyApp.Grains.Tests/PropertyTests/InventoryGrainConcurrencyTests.cs
using CsCheck;
using Xunit;

namespace MyApp.Grains.Tests.PropertyTests
{
    [Collection("TestCluster")]
    public class InventoryGrainConcurrencyTests
    {
        private readonly TestCluster _cluster;
        
        public InventoryGrainConcurrencyTests(TestClusterFixture fixture)
        {
            _cluster = fixture.Cluster;
        }
        
        [Fact]
        public void InventoryGrain_ConcurrentReservations_ShouldNeverOversell()
        {
            // Property: No matter how many concurrent reservations occur,
            // total reserved should never exceed available stock
            
            Gen.Select(
                Gen.Int[10, 100], // initial stock
                Gen.Array[5, 20](Gen.Int[1, 5]) // concurrent reservation quantities
            ).Sample(async (initialStock, reservationQuantities) =>
            {
                var productId = Guid.NewGuid().ToString();
                var grain = _cluster.GrainFactory.GetGrain<IInventoryGrain>(productId);
                
                // Set initial stock
                await grain.AddStockAsync(initialStock);
                
                // Execute concurrent reservations
                var reservationTasks = reservationQuantities.Select(async (qty, index) =>
                {
                    var orderId = Guid.NewGuid();
                    try
                    {
                        return await grain.ReserveStockAsync(orderId, qty);
                    }
                    catch
                    {
                        return false;
                    }
                }).ToArray();
                
                var results = await Task.WhenAll(reservationTasks);
                
                // Calculate total successfully reserved
                var totalReserved = reservationQuantities
                    .Where((qty, index) => results[index])
                    .Sum();
                
                // Assert property: Total reserved should not exceed initial stock
                Assert.True(totalReserved <= initialStock,
                    $"Oversold! Stock: {initialStock}, Reserved: {totalReserved}");
                
                // Assert property: Remaining stock should be non-negative
                var remainingStock = await grain.GetAvailableStockAsync();
                Assert.True(remainingStock >= 0,
                    $"Stock became negative: {remainingStock}");
                
                // Assert property: Conservation of inventory
                var reservedStock = await grain.GetTotalReservedAsync();
                Assert.Equal(initialStock, remainingStock + reservedStock);
            });
        }
    }
}
```

### Snapshot Testing with Verify.Xunit

Snapshot testing captures the output of a test and compares it against a stored "approved" snapshot. This is particularly useful for testing serialization, message formats, and grain state structures in Orleans applications.

#### Why Snapshot Testing for Orleans?

Orleans applications involve complex serialization of grain state, messages, and events. Snapshot testing helps detect unintended changes to these data structures that could break compatibility or cause serialization issues.

**Benefits**:
- Catches unintended changes to data structures and serialization
- Makes code review easier by showing exact differences
- Simplifies testing of complex object graphs
- Excellent for ensuring backward compatibility

#### Basic Snapshot Tests

```csharp
// MyApp.Contracts.Tests/SnapshotTests/SerializationSnapshotTests.cs
using Verify.Xunit;
using Xunit;

namespace MyApp.Contracts.Tests.SnapshotTests
{
    [UsesVerify]
    public class SerializationSnapshotTests
    {
        [Fact]
        public Task PlaceOrderRequest_SerializedFormat_ShouldMatchSnapshot()
        {
            // Arrange
            var request = new PlaceOrderRequest
            {
                CustomerId = "CUST-12345",
                ProductId = "PROD-67890",
                Quantity = 3,
                TotalAmount = 299.97m,
                IdempotencyKey = "IDEMPOTENT-KEY-123",
                Metadata = new Dictionary<string, string>
                {
                    ["Source"] = "WebApp",
                    ["Campaign"] = "Summer2024",
                    ["UserAgent"] = "Mozilla/5.0"
                }
            };
            
            // Act - Serialize to JSON
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            });
            
            // Assert - Compare against approved snapshot
            return Verifier.Verify(json)
                .UseDirectory("Snapshots")
                .UseFileName("PlaceOrderRequest_Format");
        }
        
        [Fact]
        public Task OrderStateDto_WithComplexData_ShouldMatchSnapshot()
        {
            // Arrange
            var orderState = new OrderStateDto
            {
                OrderId = Guid.Parse("12345678-1234-1234-1234-123456789012"),
                Status = "Placed",
                CustomerId = "CUST-12345",
                TotalAmount = 299.97m,
                CreatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                Items = new List<OrderItemDto>
                {
                    new()
                    {
                        ProductId = "PROD-001",
                        ProductName = "Widget A",
                        Quantity = 2,
                        UnitPrice = 99.99m,
                        TotalPrice = 199.98m
                    },
                    new()
                    {
                        ProductId = "PROD-002",
                        ProductName = "Gadget B",
                        Quantity = 1,
                        UnitPrice = 99.99m,
                        TotalPrice = 99.99m
                    }
                },
                ShippingAddress = new AddressDto
                {
                    Street = "123 Main St",
                    City = "Springfield",
                    State = "IL",
                    ZipCode = "62701",
                    Country = "USA"
                }
            };
            
            // Act & Assert
            return Verifier.Verify(orderState)
                .UseDirectory("Snapshots")
                .UseFileName("OrderStateDto_ComplexStructure");
        }
        
        [Fact]
        public Task OrderGrainState_Serialization_ShouldBeBackwardCompatible()
        {
            // This test ensures that grain state serialization format
            // remains compatible across versions
            
            var grainState = new OrderGrainState
            {
                OrderId = Guid.Parse("12345678-1234-1234-1234-123456789012"),
                Version = 2, // Simulate version 2 of state schema
                Status = OrderStatus.Confirmed,
                CustomerId = "CUST-12345",
                TotalAmount = 299.97m,
                Items = new[]
                {
                    new OrderItem("PROD-001", 2, 99.99m),
                    new OrderItem("PROD-002", 1, 99.99m)
                },
                StatusHistory = new[]
                {
                    new StatusChange(OrderStatus.Placed, DateTime.Parse("2024-01-15T10:30:00Z")),
                    new StatusChange(OrderStatus.Confirmed, DateTime.Parse("2024-01-15T10:35:00Z"))
                },
                // New field in version 2 (should serialize with default for v1 compatibility)
                Priority = OrderPriority.Normal
            };
            
            // Serialize using Orleans serializer
            var serializer = new JsonGrainStorageSerializer();
            var serialized = serializer.Serialize(grainState);
            
            return Verifier.Verify(serialized)
                .UseDirectory("Snapshots/BackwardCompatibility")
                .UseFileName("OrderGrainState_V2");
        }
    }
}
```

#### API Contract Snapshot Tests

```csharp
// MyApp.API.Tests/SnapshotTests/ApiContractSnapshotTests.cs
using Verify.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MyApp.API.Tests.SnapshotTests
{
    [UsesVerify]
    public class ApiContractSnapshotTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        
        public ApiContractSnapshotTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }
        
        [Fact]
        public async Task OrdersEndpoint_OpenApiSpec_ShouldMatchSnapshot()
        {
            // Retrieve OpenAPI/Swagger specification
            var response = await _client.GetAsync("/swagger/v1/swagger.json");
            response.EnsureSuccessStatusCode();
            
            var swaggerJson = await response.Content.ReadAsStringAsync();
            
            // Verify API contract hasn't changed unintentionally
            await Verifier.Verify(swaggerJson)
                .UseDirectory("Snapshots/ApiContracts")
                .UseFileName("Orders_OpenApiSpec");
        }
        
        [Fact]
        public async Task PlaceOrderEndpoint_ErrorResponse_ShouldMatchSnapshot()
        {
            // Test that error response format is stable
            var invalidRequest = new PlaceOrderRequest
            {
                CustomerId = null, // Invalid
                ProductId = "",    // Invalid
                Quantity = -1,     // Invalid
                TotalAmount = -100.00m // Invalid
            };
            
            var response = await _client.PostAsJsonAsync("/api/orders", invalidRequest);
            var errorContent = await response.Content.ReadAsStringAsync();
            
            await Verifier.Verify(new
            {
                StatusCode = (int)response.StatusCode,
                Content = errorContent
            })
            .UseDirectory("Snapshots/ApiContracts")
            .UseFileName("PlaceOrder_ValidationError");
        }
    }
}
```

### Mutation Testing for Test Quality

Mutation testing validates that your tests actually catch bugs by intentionally introducing defects (mutations) into the code and verifying that tests fail. This helps identify weak test coverage.

#### Setting Up Stryker.NET

```bash
# Install Stryker.NET globally
dotnet tool install -g dotnet-stryker

# Run mutation testing on a specific project
cd MyApp.Domain.Tests
dotnet stryker

# Generate HTML report
dotnet stryker --reporter html --reporter progress
```

#### Stryker Configuration

```json
// MyApp.Domain.Tests/stryker-config.json
{
  "stryker-config": {
    "project": "MyApp.Domain.csproj",
    "test-projects": ["MyApp.Domain.Tests.csproj"],
    "reporters": ["html", "progress", "json"],
    "thresholds": {
      "high": 80,
      "low": 60,
      "break": 50
    },
    "concurrency": 4,
    "mutation-level": "complete",
    "ignore-methods": [
      "*ToString",
      "*GetHashCode",
      "*Equals"
    ],
    "exclude-files": [
      "**/Migrations/**",
      "**/obj/**"
    ]
  }
}
```

#### Example: Before and After Mutation Testing

**Before** (Weak Test):
```csharp
// MyApp.Domain/Entities/Order.cs
public class Order
{
    public void ValidateAmount(decimal amount)
    {
        if (amount <= 0) // Mutation: Change to < 0
        {
            throw new DomainException("Amount must be positive");
        }
    }
}

// MyApp.Domain.Tests/Entities/OrderTests.cs
[Fact]
public void ValidateAmount_WithNegativeAmount_ShouldThrow()
{
    var order = new Order();
    Assert.Throws<DomainException>(() => order.ValidateAmount(-10));
}
// This test passes but doesn't catch the boundary case (amount = 0)
```

**After** (Strong Test):
```csharp
[Theory]
[InlineData(-10)]
[InlineData(-0.01)]
[InlineData(0)]    // Added to kill mutation
public void ValidateAmount_WithInvalidAmount_ShouldThrow(decimal amount)
{
    var order = new Order();
    Assert.Throws<DomainException>(() => order.ValidateAmount(amount));
}

[Theory]
[InlineData(0.01)]
[InlineData(1)]
[InlineData(100)]
public void ValidateAmount_WithValidAmount_ShouldNotThrow(decimal amount)
{
    var order = new Order();
    order.ValidateAmount(amount); // Should not throw
}
// These tests kill the mutation (change <= to <)
```

### Chaos Engineering for Orleans Clusters

Chaos engineering tests system resilience by intentionally introducing failures. For Orleans applications, this includes silo crashes, network partitions, and storage failures.

#### Chaos Testing with Orleans.TestingHost

```csharp
// MyApp.Grains.Tests/ChaosTests/SiloFailureTests.cs
using Orleans.TestingHost;
using Xunit;

namespace MyApp.Grains.Tests.ChaosTests
{
    [Collection("TestCluster")]
    [Trait("Category", "Chaos")]
    public class SiloFailureTests
    {
        private readonly TestCluster _cluster;
        
        public SiloFailureTests(TestClusterFixture fixture)
        {
            _cluster = fixture.Cluster;
        }
        
        [Fact]
        public async Task OrderGrain_ShouldRecoverAfterSiloCrash()
        {
            // Arrange - Place order on primary silo
            var orderId = Guid.NewGuid();
            var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
            
            await grain.PlaceOrderAsync(new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 1,
                TotalAmount = 100.00m
            });
            
            var stateBefore = await grain.GetOrderStatusAsync();
            Assert.Equal("Placed", stateBefore.Status);
            
            // Act - Crash the silo hosting the grain
            var primarySilo = _cluster.Primary;
            await _cluster.StopSiloAsync(primarySilo);
            
            // Wait for cluster to stabilize
            await Task.Delay(2000);
            
            // Assert - Grain should re-activate on another silo with persisted state
            var stateAfter = await grain.GetOrderStatusAsync();
            Assert.Equal("Placed", stateAfter.Status);
            Assert.Equal(stateBefore.OrderId, stateAfter.OrderId);
            Assert.Equal(stateBefore.CustomerId, stateAfter.CustomerId);
        }
        
        [Fact]
        public async Task MultiSiloCrash_ShouldMaintainClusterAvailability()
        {
            // This test requires a cluster with 5+ silos
            Skip.IfNot(_cluster.Silos.Count >= 5, "Requires 5+ silos for multi-crash test");
            
            var ordersPlaced = new List<Guid>();
            
            // Place orders across multiple silos
            for (int i = 0; i < 10; i++)
            {
                var orderId = Guid.NewGuid();
                var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
                
                await grain.PlaceOrderAsync(new PlaceOrderRequest
                {
                    CustomerId = $"CUST-{i}",
                    ProductId = "PROD-001",
                    Quantity = 1,
                    TotalAmount = 50.00m
                });
                
                ordersPlaced.Add(orderId);
            }
            
            // Crash 2 silos simultaneously
            var silosToKill = _cluster.Silos.Take(2).ToList();
            await Task.WhenAll(silosToKill.Select(s => _cluster.StopSiloAsync(s)));
            
            // Wait for cluster to recover
            await Task.Delay(3000);
            
            // Verify all orders still accessible
            foreach (var orderId in ordersPlaced)
            {
                var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
                var state = await grain.GetOrderStatusAsync();
                Assert.Equal("Placed", state.Status);
            }
        }
    }
}
```

#### Network Partition Simulation

```csharp
// MyApp.Grains.Tests/ChaosTests/NetworkPartitionTests.cs
using Orleans.TestingHost;
using Xunit;

namespace MyApp.Grains.Tests.ChaosTests
{
    [Collection("TestCluster")]
    [Trait("Category", "Chaos")]
    public class NetworkPartitionTests
    {
        [Fact]
        public async Task GrainDirectory_ShouldHandleNetworkPartition()
        {
            // Simulate scenario where grain directory becomes temporarily unavailable
            // This tests Orleans' directory caching and recovery mechanisms
            
            var cluster = new TestClusterBuilder()
                .AddSiloBuilderConfigurator<PartitionedNetworkConfigurator>()
                .Build();
            
            await cluster.DeployAsync();
            
            try
            {
                var orderId = Guid.NewGuid();
                var grain = cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
                
                // Place order while network is healthy
                await grain.PlaceOrderAsync(new PlaceOrderRequest
                {
                    CustomerId = "CUST-123",
                    ProductId = "PROD-456",
                    Quantity = 1,
                    TotalAmount = 100.00m
                });
                
                // Inject network partition (simulated via custom middleware)
                var partitionService = cluster.ServiceProvider
                    .GetRequiredService<INetworkPartitionSimulator>();
                
                await partitionService.CreatePartitionAsync(
                    cluster.Primary,
                    cluster.Silos.Skip(1).First(),
                    duration: TimeSpan.FromSeconds(5));
                
                // During partition, directory lookups should still work via cache
                var state1 = await grain.GetOrderStatusAsync();
                Assert.NotNull(state1);
                
                // Wait for partition to heal
                await Task.Delay(6000);
                
                // After healing, grain should still be accessible
                var state2 = await grain.GetOrderStatusAsync();
                Assert.Equal(state1.OrderId, state2.OrderId);
            }
            finally
            {
                await cluster.StopAllSilosAsync();
            }
        }
    }
}
```

#### Storage Failure Injection

```csharp
// MyApp.Grains.Tests/ChaosTests/StorageFailureTests.cs
using Orleans.Storage;
using Xunit;

namespace MyApp.Grains.Tests.ChaosTests
{
    [Trait("Category", "Chaos")]
    public class StorageFailureTests
    {
        [Fact]
        public async Task OrderGrain_ShouldHandleTransientStorageFailure()
        {
            // Use a custom storage provider that can inject failures
            var cluster = new TestClusterBuilder()
                .AddSiloBuilderConfigurator<FaultyStorageConfigurator>()
                .Build();
            
            await cluster.DeployAsync();
            
            try
            {
                var storageSimulator = cluster.ServiceProvider
                    .GetRequiredService<IFaultyStorageSimulator>();
                
                var orderId = Guid.NewGuid();
                var grain = cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
                
                // Configure storage to fail next 2 write attempts
                storageSimulator.InjectFailures(count: 2, failureType: StorageFailureType.WriteFailure);
                
                // Place order - should retry on storage failure
                var response = await grain.PlaceOrderAsync(new PlaceOrderRequest
                {
                    CustomerId = "CUST-123",
                    ProductId = "PROD-456",
                    Quantity = 1,
                    TotalAmount = 100.00m
                });
                
                // Verify order was eventually placed successfully after retries
                Assert.True(response.Success, "Order should succeed despite transient storage failures");
                
                var state = await grain.GetOrderStatusAsync();
                Assert.Equal("Placed", state.Status);
            }
            finally
            {
                await cluster.StopAllSilosAsync();
            }
        }
        
        [Fact]
        public async Task OrderGrain_ShouldFailGracefullyOnPermanentStorageFailure()
        {
            var cluster = new TestClusterBuilder()
                .AddSiloBuilderConfigurator<FaultyStorageConfigurator>()
                .Build();
            
            await cluster.DeployAsync();
            
            try
            {
                var storageSimulator = cluster.ServiceProvider
                    .GetRequiredService<IFaultyStorageSimulator>();
                
                // Configure storage to permanently fail
                storageSimulator.InjectFailures(count: int.MaxValue, StorageFailureType.WriteFailure);
                
                var orderId = Guid.NewGuid();
                var grain = cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
                
                // Attempt to place order
                var exception = await Assert.ThrowsAsync<OrleansException>(async () =>
                {
                    await grain.PlaceOrderAsync(new PlaceOrderRequest
                    {
                        CustomerId = "CUST-123",
                        ProductId = "PROD-456",
                        Quantity = 1,
                        TotalAmount = 100.00m
                    });
                });
                
                Assert.Contains("storage", exception.Message.ToLower());
            }
            finally
            {
                await cluster.StopAllSilosAsync();
            }
        }
    }
    
    // Helper: Faulty storage simulator for chaos testing
    public class FaultyStorageConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<IFaultyStorageSimulator, FaultyStorageSimulator>();
                services.AddSingleton<IGrainStorage, FaultyGrainStorage>();
            });
        }
    }
}
```

## Performance and Load Testing

### BenchmarkDotNet for Grain Performance

```csharp
// test/Benchmarks/Grains/OrderGrainBenchmarks.cs
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Orleans.TestingHost;

namespace MyApp.Benchmarks.Grains
{
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class OrderGrainBenchmarks
    {
        private TestCluster _cluster;
        private IOrderGrain _grain;
        private PlaceOrderRequest _request;
        
        [GlobalSetup]
        public void Setup()
        {
            _cluster = new TestClusterBuilder()
                .AddSiloBuilderConfigurator<TestSiloConfigurator>()
                .Build();
            
            _cluster.Deploy();
            
            _grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(Guid.NewGuid());
            
            _request = new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 1,
                TotalAmount = 100.00m
            };
        }
        
        [GlobalCleanup]
        public void Cleanup()
        {
            _cluster?.StopAllSilos();
        }
        
        [Benchmark(Baseline = true)]
        public async Task PlaceOrder_Baseline()
        {
            await _grain.PlaceOrderAsync(_request);
        }
        
        [Benchmark]
        public async Task PlaceOrder_WithValidation()
        {
            // Benchmark with additional validation logic
            await _grain.PlaceOrderWithValidationAsync(_request);
        }
        
        [Benchmark]
        public async Task GetOrderStatus()
        {
            await _grain.GetOrderStatusAsync();
        }
        
        [Benchmark]
        public async Task PlaceAndGetOrder()
        {
            await _grain.PlaceOrderAsync(_request);
            await _grain.GetOrderStatusAsync();
        }
    }
    
    // Run benchmarks
    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<OrderGrainBenchmarks>();
        }
    }
}
```

### Load Testing with NBomber

```csharp
// test/LoadTests/OrderApiLoadTests.cs
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;
using NBomber.Plugins.Network.Ping;

namespace MyApp.LoadTests
{
    public class OrderApiLoadTests
    {
        [Fact]
        public void OrderApi_ShouldHandleConstantLoad()
        {
            var httpFactory = HttpClientFactory.Create();
            
            var placeOrderScenario = Scenario.Create("place_order", async context =>
            {
                var request = Http.CreateRequest("POST", "http://localhost:5000/api/orders")
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(new StringContent(JsonSerializer.Serialize(new PlaceOrderRequest
                    {
                        CustomerId = $"CUST-{context.ScenarioInfo.ThreadId}",
                        ProductId = "PROD-456",
                        Quantity = 1,
                        TotalAmount = 100.00m
                    }), Encoding.UTF8, "application/json"));
                
                var response = await Http.Send(httpFactory, request);
                return response;
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(10))
            .WithLoadSimulations(
                Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2))
            );
            
            var getOrderScenario = Scenario.Create("get_order", async context =>
            {
                var orderId = GetRandomOrderId(); // Helper to get existing order
                var request = Http.CreateRequest("GET", $"http://localhost:5000/api/orders/{orderId}");
                
                var response = await Http.Send(httpFactory, request);
                return response;
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(10))
            .WithLoadSimulations(
                Simulation.Inject(rate: 200, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2))
            );
            
            NBomberRunner
                .RegisterScenarios(placeOrderScenario, getOrderScenario)
                .WithTestSuite("Order API")
                .WithTestName("Constant Load")
                .Run();
        }
        
        [Fact]
        public void OrderApi_ShouldHandleRampingLoad()
        {
            var httpFactory = HttpClientFactory.Create();
            
            var scenario = Scenario.Create("ramping_load", async context =>
            {
                var request = Http.CreateRequest("POST", "http://localhost:5000/api/orders")
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(new StringContent(JsonSerializer.Serialize(new PlaceOrderRequest
                    {
                        CustomerId = $"CUST-{context.ScenarioInfo.ThreadId}",
                        ProductId = "PROD-456",
                        Quantity = 1,
                        TotalAmount = 100.00m
                    }), Encoding.UTF8, "application/json"));
                
                var response = await Http.Send(httpFactory, request);
                return response;
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(10))
            .WithLoadSimulations(
                // Ramp up from 10 to 200 requests/sec over 2 minutes
                Simulation.RampingInject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)),
                Simulation.RampingInject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)),
                Simulation.RampingInject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)),
                Simulation.RampingInject(rate: 200, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
            );
            
            var result = NBomberRunner
                .RegisterScenarios(scenario)
                .WithTestSuite("Order API")
                .WithTestName("Ramping Load")
                .Run();
            
            // Assert performance thresholds
            var stats = result.ScenarioStats[0];
            Assert.True(stats.Ok.Request.RPS > 150, "Should handle >150 req/sec");
            Assert.True(stats.Ok.Latency.Percent99 < 500, "P99 latency should be <500ms");
        }
    }
}
```

## Modern CI/CD Integration

### GitHub Actions Workflow with Modern Testing

```yaml
# .github/workflows/comprehensive-test.yml
name: Comprehensive Test Suite

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main, develop]
  schedule:
    # Run nightly for chaos and load tests
    - cron: '0 2 * * *'

jobs:
  unit-tests:
    name: Unit Tests (Fast)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Run unit tests
        run: |
          dotnet test \
            --filter "Category!=Integration&Category!=E2E&Category!=Chaos&Category!=Load" \
            --logger "trx;LogFileName=test-results.trx" \
            --logger "GitHubActions" \
            --collect:"XPlat Code Coverage" \
            -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
      
      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: unit-test-results
          path: '**/TestResults/*.trx'
      
      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v4
        with:
          files: '**/coverage.opencover.xml'
          flags: unittests
          name: unit-test-coverage

  property-based-tests:
    name: Property-Based Tests (CsCheck)
    runs-on: ubuntu-latest
    needs: unit-tests
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Run property tests
        run: |
          dotnet test \
            --filter "Category=Property" \
            --logger "trx;LogFileName=property-test-results.trx" \
            --logger "GitHubActions"
      
      - name: Upload property test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: property-test-results
          path: '**/TestResults/*.trx'

  mutation-testing:
    name: Mutation Testing (Stryker)
    runs-on: ubuntu-latest
    needs: unit-tests
    if: github.event_name == 'pull_request'
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0 # Required for Stryker diff
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Install Stryker.NET
        run: dotnet tool install -g dotnet-stryker
      
      - name: Run mutation testing
        run: |
          cd MyApp.Domain.Tests
          dotnet stryker \
            --reporter html \
            --reporter json \
            --reporter progress \
            --threshold-break 50 \
            --since main
      
      - name: Upload mutation report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: mutation-test-report
          path: '**/StrykerOutput/**'
      
      - name: Comment PR with mutation score
        if: github.event_name == 'pull_request'
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            const report = JSON.parse(fs.readFileSync('MyApp.Domain.Tests/StrykerOutput/mutation-report.json'));
            const score = report.mutationScore;
            
            await github.rest.issues.createComment({
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number: context.issue.number,
              body: `## Mutation Testing Results\n\n**Mutation Score:** ${score.toFixed(2)}%\n\nView detailed report in artifacts.`
            });

  integration-tests:
    name: Integration Tests
    runs-on: ubuntu-latest
    needs: unit-tests
    services:
      azurite:
        image: mcr.microsoft.com/azure-storage/azurite
        ports:
          - 10000:10000
          - 10001:10001
          - 10002:10002
      redis:
        image: redis:7
        ports:
          - 6379:6379
      postgres:
        image: postgres:16
        env:
          POSTGRES_PASSWORD: testpassword
        ports:
          - 5432:5432
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Run integration tests
        env:
          AZURE_STORAGE_CONNECTION_STRING: "UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://localhost"
          REDIS_CONNECTION_STRING: "localhost:6379"
          POSTGRES_CONNECTION_STRING: "Host=localhost;Port=5432;Database=orleans_test;Username=postgres;Password=testpassword"
        run: |
          dotnet test \
            --filter "Category=Integration" \
            --logger "trx;LogFileName=integration-test-results.trx" \
            --logger "GitHubActions" \
            --collect:"XPlat Code Coverage"
      
      - name: Upload integration test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: integration-test-results
          path: '**/TestResults/*.trx'

  chaos-tests:
    name: Chaos Engineering Tests
    runs-on: ubuntu-latest
    needs: integration-tests
    if: github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Run chaos tests
        run: |
          dotnet test \
            --filter "Category=Chaos" \
            --logger "trx;LogFileName=chaos-test-results.trx" \
            --logger "GitHubActions"
      
      - name: Upload chaos test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: chaos-test-results
          path: '**/TestResults/*.trx'

  performance-benchmarks:
    name: Performance Benchmarks
    runs-on: ubuntu-latest
    needs: integration-tests
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Run benchmarks
        run: |
          cd test/Benchmarks
          dotnet run -c Release --exporters json --filter "*"
      
      - name: Upload benchmark results
        uses: actions/upload-artifact@v4
        with:
          name: benchmark-results
          path: 'test/Benchmarks/BenchmarkDotNet.Artifacts/results/*'
      
      - name: Store benchmark results
        uses: benchmark-action/github-action-benchmark@v1
        with:
          tool: 'benchmarkdotnet'
          output-file-path: test/Benchmarks/BenchmarkDotNet.Artifacts/results/results.json
          github-token: ${{ secrets.GITHUB_TOKEN }}
          auto-push: true

  load-tests:
    name: Load Tests
    runs-on: ubuntu-latest
    needs: integration-tests
    if: github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Start application
        run: |
          cd src/MyApp.API
          dotnet run --configuration Release &
          sleep 30 # Wait for app to start
      
      - name: Run load tests
        run: |
          dotnet test test/LoadTests \
            --filter "Category=Load" \
            --logger "trx;LogFileName=load-test-results.trx"
      
      - name: Upload load test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: load-test-results
          path: '**/TestResults/*.trx'

  snapshot-verification:
    name: Snapshot Verification
    runs-on: ubuntu-latest
    needs: unit-tests
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Run snapshot tests
        run: |
          dotnet test \
            --filter "Category=Snapshot" \
            --logger "trx;LogFileName=snapshot-test-results.trx" \
            --logger "GitHubActions"
      
      - name: Check for snapshot changes
        if: failure()
        run: |
          echo "Snapshot tests failed. Review snapshot changes and approve if intentional."
          git diff --name-only **/*.received.*
      
      - name: Upload received snapshots
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: received-snapshots
          path: '**/*.received.*'

  test-report:
    name: Generate Test Report
    runs-on: ubuntu-latest
    needs: [unit-tests, integration-tests, property-based-tests]
    if: always()
    steps:
      - name: Download all test results
        uses: actions/download-artifact@v4
        with:
          path: test-results
      
      - name: Publish test report
        uses: dorny/test-reporter@v1
        with:
          name: Test Results
          path: 'test-results/**/*.trx'
          reporter: 'dotnet-trx'
          fail-on-error: 'false'
```

## Testing Best Practices for Orleans Applications

### Grain Testing Patterns

**Pattern 1: Use Test Doubles for External Dependencies**

```csharp
// Good: Mock external services in grain tests
public class OrderGrain : Grain, IOrderGrain
{
    private readonly IPaymentService _paymentService; // Injected
    
    public OrderGrain(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }
}

// In tests, use mock
siloBuilder.ConfigureServices(services =>
{
    services.AddSingleton<IPaymentService, MockPaymentService>();
});
```

**Pattern 2: Test Grain Lifecycle Events**

```csharp
[Fact]
public async Task Grain_OnActivate_ShouldLoadState()
{
    var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(knownOrderId);
    
    // First call triggers OnActivateAsync
    var state = await grain.GetOrderStatusAsync();
    
    // Verify state was loaded from storage
    Assert.NotNull(state);
    Assert.Equal(expectedOrderId, state.OrderId);
}
```

**Pattern 3: Test Idempotency**

```csharp
[Fact]
public async Task PlaceOrder_WithSameIdempotencyKey_ShouldBeIdempotent()
{
    var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(Guid.NewGuid());
    var idempotencyKey = Guid.NewGuid().ToString();
    
    var request = new PlaceOrderRequest
    {
        IdempotencyKey = idempotencyKey,
        CustomerId = "CUST-123",
        ProductId = "PROD-456",
        Quantity = 1,
        TotalAmount = 100.00m
    };
    
    var response1 = await grain.PlaceOrderAsync(request);
    var response2 = await grain.PlaceOrderAsync(request);
    
    Assert.Equal(response1.OrderId, response2.OrderId);
    Assert.Equal(response1.Success, response2.Success);
}
```

### Testing Anti-Patterns to Avoid

**Anti-Pattern 1: Testing Implementation Details**

```csharp
// Bad: Testing private methods or internal state
[Fact]
public void ValidateOrder_PrivateMethod_ShouldWork()
{
    var grain = new OrderGrain(); // Don't instantiate grains directly
    var method = typeof(OrderGrain).GetMethod("ValidateOrder", BindingFlags.NonPublic);
    // Don't test private methods
}

// Good: Test through public interface
[Fact]
public async Task PlaceOrder_WithInvalidData_ShouldReturnValidationError()
{
    var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(Guid.NewGuid());
    var response = await grain.PlaceOrderAsync(invalidRequest);
    Assert.False(response.Success);
    Assert.Contains("validation", response.Message);
}
```

**Anti-Pattern 2: Over-Mocking**

```csharp
// Bad: Mocking everything, testing nothing
var mockStorage = new Mock<IGrainStorage>();
var mockSerializer = new Mock<IGrainStorageSerializer>();
var mockLogger = new Mock<ILogger>();
var mockFactory = new Mock<IGrainFactory>();
// ... testing mocks, not real behavior

// Good: Use real Orleans infrastructure with TestingHost
var cluster = new TestClusterBuilder().Build();
// Test real grain behavior with real Orleans runtime
```

**Anti-Pattern 3: Slow Integration Tests**

```csharp
// Bad: Creating new cluster for each test
public class SlowTests
{
    [Fact]
    public async Task Test1()
    {
        var cluster = new TestClusterBuilder().Build();
        await cluster.DeployAsync();
        // ... test
        await cluster.StopAllSilosAsync();
    }
    
    [Fact]
    public async Task Test2()
    {
        var cluster = new TestClusterBuilder().Build(); // Slow!
        // ...
    }
}

// Good: Share cluster across test class
[Collection("TestCluster")]
public class FastTests
{
    private readonly TestCluster _cluster;
    
    public FastTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster; // Reuse cluster
    }
}
```

## Summary and Recommendations

### Testing Maturity Levels

**Level 1: Basic Testing**
- Unit tests for domain logic
- Basic grain integration tests with Orleans.TestingHost
- Manual testing of API endpoints

**Level 2: Intermediate Testing**
- Comprehensive unit and integration test coverage
- Automated CI/CD pipeline
- Code coverage tracking
- Basic performance testing

**Level 3: Advanced Testing** (Target State)
- Property-based testing for complex scenarios
- Snapshot testing for contract stability
- Mutation testing for test quality validation
- Chaos engineering for resilience testing
- Comprehensive load testing
- Automated performance regression detection

### Recommended Testing Strategy for Orleans Projects

1. **Start with Fast Unit Tests** (80% of tests)
   - Test domain logic without Orleans dependencies
   - Use mocks/stubs for external services
   - Target <1 minute execution time for full suite

2. **Add Orleans Integration Tests** (15% of tests)
   - Use Orleans.TestingHost for grain tests
   - Test grain lifecycle, state persistence, messaging
   - Share test clusters across test classes

3. **Include Property-Based Tests** (3% of tests)
   - Use CsCheck for complex state machines
   - Validate invariants across random inputs
   - Focus on concurrent scenarios

4. **Implement Snapshot Tests** (1% of tests)
   - Use Verify.Xunit for contract stability
   - Catch unintended serialization changes
   - Ensure backward compatibility

5. **Run Chaos Tests Nightly** (<1% of tests)
   - Test silo failures and recovery
   - Simulate network partitions
   - Inject storage failures

6. **Automate Performance Testing**
   - Run benchmarks on main branch merges
   - Detect performance regressions automatically
   - Schedule load tests nightly

### Next Steps

1. **Immediate Actions** (Week 1-2):
   - Set up Orleans.TestingHost fixtures for existing grain tests
   - Configure GitHub Actions workflow with unit and integration tests
   - Add code coverage reporting

2. **Short-term Goals** (Month 1):
   - Achieve 80%+ unit test coverage for domain layer
   - Add property-based tests for critical state machines
   - Implement snapshot tests for API contracts

3. **Medium-term Goals** (Quarter 1):
   - Introduce mutation testing in CI/CD pipeline
   - Set up chaos testing for resilience validation
   - Establish performance baselines with BenchmarkDotNet

4. **Long-term Goals** (Quarter 2+):
   - Achieve 90%+ overall test coverage
   - Automate load testing in staging environment
   - Integrate advanced observability with test feedback

By following this modernization guidance and progressively adopting advanced testing techniques, Orleans applications will achieve high confidence in correctness, resilience, and performance while maintaining rapid development velocity.

Task completed: Created comprehensive testing modernization guidance (08-testing-modernization-guidance.md) with modern patterns, property-based testing, snapshot testing, chaos engineering, performance testing, and CI/CD integration examples for Orleans applications.
