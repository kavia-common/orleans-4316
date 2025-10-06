# Testing Strategy for Orleans Applications

## Overview

This document outlines a comprehensive testing strategy for Orleans-based applications, covering all layers from pure domain logic to end-to-end distributed system tests. The strategy emphasizes isolation, fast feedback loops, and realistic integration testing using Orleans.TestingHost.

Testing Orleans applications requires a layered approach that mirrors the modular architecture. Each layer has specific testing concerns, tools, and patterns that maximize confidence while minimizing test execution time and complexity.

## Testing Pyramid

The Orleans testing pyramid emphasizes fast, isolated tests at the base with progressively fewer, slower, more integrated tests at higher levels:

```
                    E2E Tests (API → Grains → Storage → Streams)
                    ~10 tests, ~5 min execution
                   ╱                                          ╲
                  ╱                                            ╲
                 ╱      Contract/Compatibility Tests           ╲
                ╱       ~50 tests, ~2 min execution             ╲
               ╱                                                  ╲
              ╱         Grain Integration Tests                   ╲
             ╱          (Orleans.TestingHost)                      ╲
            ╱           ~200 tests, ~5 min execution                ╲
           ╱                                                          ╲
          ╱              Provider Tests                               ╲
         ╱               (Memory vs External)                          ╲
        ╱                ~100 tests, ~3 min execution                   ╲
       ╱                                                                  ╲
      ╱____________________________________________________________________╲
     ╱                  Domain & Application Unit Tests                    ╲
    ╱                   (No Orleans Dependencies)                           ╲
   ╱                    ~1000 tests, <1 min execution                       ╲
  ╱________________________________________________________________________ ╲
```

## Layer 1: Domain and Application Unit Tests (No Orleans)

### Objectives

Test pure business logic without any Orleans dependencies. These tests should be blazingly fast (<100ms per test) and provide immediate feedback during development.

### Test Structure

```csharp
// MyApp.Domain.Tests/Entities/OrderTests.cs
using Xunit;
using MyApp.Domain.Entities;
using MyApp.Domain.Exceptions;

namespace MyApp.Domain.Tests.Entities
{
    public class OrderTests
    {
        [Fact]
        public void PlaceOrder_WithValidData_ShouldCreateOrder()
        {
            // Arrange
            var order = new Order();
            var customerId = "CUST-123";
            var amount = 99.99m;
            
            // Act
            order.PlaceOrder(customerId, amount);
            
            // Assert
            Assert.NotEqual(Guid.Empty, order.OrderId);
            Assert.Equal(customerId, order.CustomerId);
            Assert.Equal(amount, order.TotalAmount);
            Assert.Equal(OrderStatus.Placed, order.Status);
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(-10.50)]
        public void PlaceOrder_WithInvalidAmount_ShouldThrowDomainException(decimal invalidAmount)
        {
            // Arrange
            var order = new Order();
            
            // Act & Assert
            var exception = Assert.Throws<DomainException>(() =>
                order.PlaceOrder("CUST-123", invalidAmount));
            
            Assert.Contains("amount must be positive", exception.Message);
        }
        
        [Fact]
        public void CancelOrder_WhenShipped_ShouldThrowDomainException()
        {
            // Arrange
            var order = new Order();
            order.PlaceOrder("CUST-123", 50.00m);
            order.Ship(); // Simulate shipping
            
            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => order.Cancel());
            Assert.Contains("Cannot cancel shipped orders", exception.Message);
        }
        
        [Fact]
        public void CancelOrder_WhenPlaced_ShouldUpdateStatus()
        {
            // Arrange
            var order = new Order();
            order.PlaceOrder("CUST-123", 50.00m);
            
            // Act
            order.Cancel();
            
            // Assert
            Assert.Equal(OrderStatus.Cancelled, order.Status);
        }
    }
}
```

### Application Layer Unit Tests

Test use case orchestration with mocked dependencies:

```csharp
// MyApp.Application.Tests/UseCases/PlaceOrderUseCaseTests.cs
using Xunit;
using Moq;
using MyApp.Application.UseCases;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Application.Tests.UseCases
{
    public class PlaceOrderUseCaseTests
    {
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly Mock<IInventoryService> _mockInventoryService;
        private readonly Mock<IPaymentService> _mockPaymentService;
        private readonly PlaceOrderUseCase _useCase;
        
        public PlaceOrderUseCaseTests()
        {
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockInventoryService = new Mock<IInventoryService>();
            _mockPaymentService = new Mock<IPaymentService>();
            
            _useCase = new PlaceOrderUseCase(
                _mockOrderRepository.Object,
                _mockInventoryService.Object,
                _mockPaymentService.Object);
        }
        
        [Fact]
        public async Task ExecuteAsync_WithAvailableInventory_ShouldPlaceOrder()
        {
            // Arrange
            var command = new PlaceOrderCommand
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 2,
                TotalAmount = 99.99m
            };
            
            _mockInventoryService
                .Setup(x => x.CheckAvailabilityAsync(command.ProductId, command.Quantity))
                .ReturnsAsync(true);
            
            _mockPaymentService
                .Setup(x => x.ProcessPaymentAsync(command.CustomerId, command.TotalAmount))
                .ReturnsAsync(new PaymentResult { Success = true, TransactionId = "TXN-789" });
            
            _mockOrderRepository
                .Setup(x => x.SaveAsync(It.IsAny<Order>()))
                .Returns(Task.CompletedTask);
            
            // Act
            var result = await _useCase.ExecuteAsync(command);
            
            // Assert
            Assert.True(result.Success);
            Assert.NotEqual(Guid.Empty, result.OrderId);
            
            // Verify interactions
            _mockInventoryService.Verify(
                x => x.CheckAvailabilityAsync(command.ProductId, command.Quantity),
                Times.Once);
            
            _mockPaymentService.Verify(
                x => x.ProcessPaymentAsync(command.CustomerId, command.TotalAmount),
                Times.Once);
            
            _mockOrderRepository.Verify(
                x => x.SaveAsync(It.Is<Order>(o => 
                    o.CustomerId == command.CustomerId && 
                    o.TotalAmount == command.TotalAmount)),
                Times.Once);
        }
        
        [Fact]
        public async Task ExecuteAsync_WithUnavailableInventory_ShouldReturnFailure()
        {
            // Arrange
            var command = new PlaceOrderCommand
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 2,
                TotalAmount = 99.99m
            };
            
            _mockInventoryService
                .Setup(x => x.CheckAvailabilityAsync(command.ProductId, command.Quantity))
                .ReturnsAsync(false);
            
            // Act
            var result = await _useCase.ExecuteAsync(command);
            
            // Assert
            Assert.False(result.Success);
            Assert.Contains("Product not available", result.Message);
            
            // Should not attempt payment or save order
            _mockPaymentService.Verify(
                x => x.ProcessPaymentAsync(It.IsAny<string>(), It.IsAny<decimal>()),
                Times.Never);
            
            _mockOrderRepository.Verify(
                x => x.SaveAsync(It.IsAny<Order>()),
                Times.Never);
        }
        
        [Fact]
        public async Task ExecuteAsync_WhenPaymentFails_ShouldReturnFailure()
        {
            // Arrange
            var command = new PlaceOrderCommand
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 2,
                TotalAmount = 99.99m
            };
            
            _mockInventoryService
                .Setup(x => x.CheckAvailabilityAsync(command.ProductId, command.Quantity))
                .ReturnsAsync(true);
            
            _mockPaymentService
                .Setup(x => x.ProcessPaymentAsync(command.CustomerId, command.TotalAmount))
                .ReturnsAsync(new PaymentResult 
                { 
                    Success = false, 
                    Message = "Insufficient funds" 
                });
            
            // Act
            var result = await _useCase.ExecuteAsync(command);
            
            // Assert
            Assert.False(result.Success);
            Assert.Contains("Insufficient funds", result.Message);
            
            // Should not save order if payment failed
            _mockOrderRepository.Verify(
                x => x.SaveAsync(It.IsAny<Order>()),
                Times.Never);
        }
    }
}
```

### Best Practices for Unit Tests

1. **No Orleans Dependencies**: Use interfaces and dependency injection
2. **Fast Execution**: Entire suite should run in <1 minute
3. **Isolated**: Each test independent, no shared state
4. **Descriptive Names**: Method names describe scenario and expected outcome
5. **AAA Pattern**: Arrange, Act, Assert structure for clarity
6. **Theory Tests**: Use `[Theory]` for parameterized tests with multiple inputs

## Layer 2: Grain Integration Tests with Orleans.TestingHost

### Objectives

Test grain implementations with real Orleans runtime, storage, and messaging infrastructure. Verify grain lifecycle, state persistence, and grain-to-grain communication.

### Test Cluster Setup

```csharp
// MyApp.Grains.Tests/TestClusterFixture.cs
using Orleans.TestingHost;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Grains.Tests
{
    /// <summary>
    /// Shared test cluster fixture that initializes once per test collection.
    /// This improves performance by reusing the cluster across multiple tests.
    /// </summary>
    public class TestClusterFixture : IDisposable
    {
        public TestCluster Cluster { get; }
        
        public TestClusterFixture()
        {
            var builder = new TestClusterBuilder();
            
            builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
            builder.AddClientBuilderConfigurator<TestClientConfigurator>();
            
            Cluster = builder.Build();
            Cluster.Deploy();
        }
        
        public void Dispose()
        {
            Cluster?.StopAllSilos();
        }
        
        private class TestSiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                // Configure in-memory storage for testing
                siloBuilder.AddMemoryGrainStorage("orders");
                siloBuilder.AddMemoryGrainStorage("inventory");
                siloBuilder.AddMemoryGrainStorage("PubSubStore");
                
                // Add memory streams for testing
                siloBuilder.AddMemoryStreams<DefaultMemoryMessageBodySerializer>("orders");
                
                // Register grain implementations
                siloBuilder.ConfigureApplicationParts(parts =>
                    parts.AddApplicationPart(typeof(OrderGrain).Assembly)
                         .WithReferences());
                
                // Register application services
                siloBuilder.ConfigureServices(services =>
                {
                    services.AddScoped<PlaceOrderUseCase>();
                    services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
                    services.AddScoped<IInventoryService, MockInventoryService>();
                    services.AddScoped<IPaymentService, MockPaymentService>();
                });
            }
        }
        
        private class TestClientConfigurator : IClientBuilderConfigurator
        {
            public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
            {
                clientBuilder.ConfigureApplicationParts(parts =>
                    parts.AddApplicationPart(typeof(IOrderGrain).Assembly));
            }
        }
    }
    
    /// <summary>
    /// Define test collection to share fixture across test classes.
    /// This prevents creating/destroying the cluster for each test class.
    /// </summary>
    [CollectionDefinition("TestCluster")]
    public class TestClusterCollection : ICollectionFixture<TestClusterFixture>
    {
    }
}
```

### Grain State Tests

```csharp
// MyApp.Grains.Tests/OrderGrainTests.cs
using Xunit;
using Orleans.TestingHost;
using MyApp.GrainContracts;
using MyApp.Contracts.Orders;

namespace MyApp.Grains.Tests
{
    [Collection("TestCluster")]
    public class OrderGrainTests
    {
        private readonly TestCluster _cluster;
        
        public OrderGrainTests(TestClusterFixture fixture)
        {
            _cluster = fixture.Cluster;
        }
        
        [Fact]
        public async Task PlaceOrder_ShouldPersistState()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
            
            var request = new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 2,
                TotalAmount = 99.99m
            };
            
            // Act
            var response = await grain.PlaceOrderAsync(request);
            
            // Assert
            Assert.True(response.Success);
            Assert.Equal(orderId, response.OrderId);
            
            // Verify state was persisted by reading it back
            var state = await grain.GetOrderStatusAsync();
            Assert.Equal(orderId, state.OrderId);
            Assert.Equal("Placed", state.Status);
            Assert.Equal(request.CustomerId, state.CustomerId);
            Assert.Equal(request.TotalAmount, state.TotalAmount);
        }
        
        [Fact]
        public async Task GetOrderStatus_ForNewGrain_ShouldReturnEmptyState()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
            
            // Act
            var state = await grain.GetOrderStatusAsync();
            
            // Assert
            Assert.Equal(Guid.Empty, state.OrderId);
            Assert.Null(state.Status);
        }
        
        [Fact]
        public async Task CancelOrder_ShouldUpdateState()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
            
            await grain.PlaceOrderAsync(new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 1,
                TotalAmount = 50.00m
            });
            
            // Act
            await grain.CancelOrderAsync();
            
            // Assert
            var state = await grain.GetOrderStatusAsync();
            Assert.Equal("Cancelled", state.Status);
        }
        
        [Fact]
        public async Task PlaceOrder_WhenInventoryUnavailable_ShouldReturnFailure()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
            
            var request = new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = "UNAVAILABLE-PRODUCT", // Mock service recognizes this
                Quantity = 1,
                TotalAmount = 50.00m
            };
            
            // Act
            var response = await grain.PlaceOrderAsync(request);
            
            // Assert
            Assert.False(response.Success);
            Assert.Contains("not available", response.Message);
            
            // Verify state was not persisted
            var state = await grain.GetOrderStatusAsync();
            Assert.Equal(Guid.Empty, state.OrderId);
        }
    }
}
```

### Grain-to-Grain Communication Tests

```csharp
// MyApp.Grains.Tests/OrderWorkflowTests.cs
using Xunit;
using Orleans.TestingHost;

namespace MyApp.Grains.Tests
{
    [Collection("TestCluster")]
    public class OrderWorkflowTests
    {
        private readonly TestCluster _cluster;
        
        public OrderWorkflowTests(TestClusterFixture fixture)
        {
            _cluster = fixture.Cluster;
        }
        
        [Fact]
        public async Task PlaceOrder_ShouldReserveInventory()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var productId = "PROD-456";
            var quantity = 2;
            
            var orderGrain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
            var inventoryGrain = _cluster.GrainFactory.GetGrain<IInventoryGrain>(productId);
            
            // Set initial inventory
            await inventoryGrain.AddStockAsync(10);
            var initialStock = await inventoryGrain.GetAvailableStockAsync();
            
            // Act
            var response = await orderGrain.PlaceOrderAsync(new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = productId,
                Quantity = quantity,
                TotalAmount = 99.99m
            });
            
            // Assert
            Assert.True(response.Success);
            
            // Verify inventory was reserved
            var finalStock = await inventoryGrain.GetAvailableStockAsync();
            Assert.Equal(initialStock - quantity, finalStock);
            
            var reservations = await inventoryGrain.GetReservationsAsync();
            Assert.Contains(orderId, reservations.Keys);
            Assert.Equal(quantity, reservations[orderId]);
        }
        
        [Fact]
        public async Task CancelOrder_ShouldReleaseInventory()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var productId = "PROD-456";
            var quantity = 2;
            
            var orderGrain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
            var inventoryGrain = _cluster.GrainFactory.GetGrain<IInventoryGrain>(productId);
            
            await inventoryGrain.AddStockAsync(10);
            await orderGrain.PlaceOrderAsync(new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = productId,
                Quantity = quantity,
                TotalAmount = 99.99m
            });
            
            var stockAfterOrder = await inventoryGrain.GetAvailableStockAsync();
            
            // Act
            await orderGrain.CancelOrderAsync();
            
            // Assert
            var finalStock = await inventoryGrain.GetAvailableStockAsync();
            Assert.Equal(stockAfterOrder + quantity, finalStock);
            
            var reservations = await inventoryGrain.GetReservationsAsync();
            Assert.DoesNotContain(orderId, reservations.Keys);
        }
    }
}
```

### Grain Lifecycle Tests

```csharp
// MyApp.Grains.Tests/GrainLifecycleTests.cs
using Xunit;
using Orleans.TestingHost;

namespace MyApp.Grains.Tests
{
    [Collection("TestCluster")]
    public class GrainLifecycleTests
    {
        private readonly TestCluster _cluster;
        
        public GrainLifecycleTests(TestClusterFixture fixture)
        {
            _cluster = fixture.Cluster;
        }
        
        [Fact]
        public async Task OnActivateAsync_ShouldInitializeGrain()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
            
            // Act - First call triggers OnActivateAsync
            var state = await grain.GetOrderStatusAsync();
            
            // Assert - Verify grain was properly initialized
            Assert.NotNull(state);
        }
        
        [Fact]
        public async Task DeactivateOnIdle_ShouldPreserveState()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
            
            await grain.PlaceOrderAsync(new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 1,
                TotalAmount = 50.00m
            });
            
            // Act - Force deactivation
            await grain.DeactivateAsync();
            
            // Wait for deactivation to complete
            await Task.Delay(500);
            
            // Re-activate by calling grain again
            var state = await grain.GetOrderStatusAsync();
            
            // Assert - State should be preserved across activation boundary
            Assert.NotEqual(Guid.Empty, state.OrderId);
            Assert.Equal("Placed", state.Status);
        }
    }
}
```

### Stream Tests

```csharp
// MyApp.Grains.Tests/OrderStreamTests.cs
using Xunit;
using Orleans.TestingHost;
using Orleans.Streams;
using Orleans;

namespace MyApp.Grains.Tests
{
    [Collection("TestCluster")]
    public class OrderStreamTests
    {
        private readonly TestCluster _cluster;
        
        public OrderStreamTests(TestClusterFixture fixture)
        {
            _cluster = fixture.Cluster;
        }
        
        [Fact]
        public async Task PlaceOrder_ShouldPublishToStream()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
            
            var streamProvider = _cluster.Client.GetStreamProvider("orders");
            var stream = streamProvider.GetStream<OrderEvent>(
                StreamId.Create("order-events", "global"));
            
            var receivedEvents = new List<OrderEvent>();
            var subscription = await stream.SubscribeAsync((evt, token) =>
            {
                receivedEvents.Add(evt);
                return Task.CompletedTask;
            });
            
            // Act
            await grain.PlaceOrderAsync(new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 1,
                TotalAmount = 50.00m
            });
            
            // Wait for stream processing
            await Task.Delay(500);
            
            // Assert
            Assert.Single(receivedEvents);
            Assert.Equal("OrderPlaced", receivedEvents[0].EventType);
            Assert.Equal(orderId, receivedEvents[0].OrderId);
            
            // Cleanup
            await subscription.UnsubscribeAsync();
        }
        
        [Fact]
        public async Task StreamSubscriber_ShouldProcessOrderEvents()
        {
            // Arrange
            var streamProvider = _cluster.Client.GetStreamProvider("orders");
            var stream = streamProvider.GetStream<OrderEvent>(
                StreamId.Create("order-events", "test"));
            
            var processorGrain = _cluster.GrainFactory.GetGrain<IOrderEventProcessorGrain>(Guid.NewGuid());
            
            // Act - Subscribe grain to stream
            await processorGrain.SubscribeToOrderEventsAsync();
            
            // Publish events
            await stream.OnNextAsync(new OrderEvent
            {
                EventType = "OrderPlaced",
                OrderId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow
            });
            
            await stream.OnNextAsync(new OrderEvent
            {
                EventType = "OrderCancelled",
                OrderId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow
            });
            
            // Wait for processing
            await Task.Delay(1000);
            
            // Assert
            var processedCount = await processorGrain.GetProcessedEventCountAsync();
            Assert.Equal(2, processedCount);
        }
    }
}
```

## Layer 3: Provider Tests (Memory vs External)

### Objectives

Test storage and streaming providers in isolation, starting with in-memory implementations and progressing to external dependencies.

### Memory Provider Tests

```csharp
// MyApp.Infrastructure.Tests/Storage/OrderStorageTests.cs
using Xunit;
using Orleans.Runtime;
using Orleans.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MyApp.Infrastructure.Tests.Storage
{
    public class OrderStorageTests
    {
        private readonly IGrainStorage _storage;
        private readonly IGrainStorageSerializer _serializer;
        
        public OrderStorageTests()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IGrainStorageSerializer, JsonGrainStorageSerializer>();
            var serviceProvider = services.BuildServiceProvider();
            
            _serializer = serviceProvider.GetRequiredService<IGrainStorageSerializer>();
            _storage = new MemoryGrainStorage("test-storage", NullLoggerFactory.Instance);
        }
        
        [Fact]
        public async Task ReadStateAsync_ForNewGrain_ShouldReturnEmptyState()
        {
            // Arrange
            var grainId = GrainId.Create("order", Guid.NewGuid().ToString());
            var grainState = new GrainState<OrderState> { State = new OrderState() };
            
            // Act
            await _storage.ReadStateAsync("OrderGrain", grainId, grainState);
            
            // Assert
            Assert.Null(grainState.ETag);
            Assert.Equal(Guid.Empty, grainState.State.OrderId);
        }
        
        [Fact]
        public async Task WriteStateAsync_ShouldPersistState()
        {
            // Arrange
            var grainId = GrainId.Create("order", Guid.NewGuid().ToString());
            var orderId = Guid.NewGuid();
            var grainState = new GrainState<OrderState>
            {
                State = new OrderState
                {
                    OrderId = orderId,
                    Status = OrderStatus.Placed,
                    CustomerId = "CUST-123",
                    TotalAmount = 99.99m
                }
            };
            
            // Act
            await _storage.WriteStateAsync("OrderGrain", grainId, grainState);
            
            // Assert - Read back to verify
            var readState = new GrainState<OrderState> { State = new OrderState() };
            await _storage.ReadStateAsync("OrderGrain", grainId, readState);
            
            Assert.NotNull(readState.ETag);
            Assert.Equal(orderId, readState.State.OrderId);
            Assert.Equal(OrderStatus.Placed, readState.State.Status);
            Assert.Equal("CUST-123", readState.State.CustomerId);
            Assert.Equal(99.99m, readState.State.TotalAmount);
        }
        
        [Fact]
        public async Task ClearStateAsync_ShouldRemoveState()
        {
            // Arrange
            var grainId = GrainId.Create("order", Guid.NewGuid().ToString());
            var grainState = new GrainState<OrderState>
            {
                State = new OrderState
                {
                    OrderId = Guid.NewGuid(),
                    Status = OrderStatus.Placed
                }
            };
            
            await _storage.WriteStateAsync("OrderGrain", grainId, grainState);
            
            // Act
            await _storage.ClearStateAsync("OrderGrain", grainId, grainState);
            
            // Assert
            var readState = new GrainState<OrderState> { State = new OrderState() };
            await _storage.ReadStateAsync("OrderGrain", grainId, readState);
            
            Assert.Null(readState.ETag);
            Assert.Equal(Guid.Empty, readState.State.OrderId);
        }
        
        [Fact]
        public async Task WriteStateAsync_WithConcurrentModification_ShouldHandleETag()
        {
            // Arrange
            var grainId = GrainId.Create("order", Guid.NewGuid().ToString());
            
            var state1 = new GrainState<OrderState>
            {
                State = new OrderState { OrderId = Guid.NewGuid() }
            };
            
            await _storage.WriteStateAsync("OrderGrain", grainId, state1);
            
            // Simulate two concurrent modifications
            var state2 = new GrainState<OrderState>
            {
                State = new OrderState { OrderId = state1.State.OrderId, Status = OrderStatus.Confirmed },
                ETag = state1.ETag
            };
            
            var state3 = new GrainState<OrderState>
            {
                State = new OrderState { OrderId = state1.State.OrderId, Status = OrderStatus.Cancelled },
                ETag = state1.ETag // Same ETag - concurrent modification
            };
            
            // Act
            await _storage.WriteStateAsync("OrderGrain", grainId, state2);
            
            // Assert - Third write should detect ETag mismatch
            await Assert.ThrowsAsync<InconsistentStateException>(() =>
                _storage.WriteStateAsync("OrderGrain", grainId, state3));
        }
    }
}
```

### External Provider Integration Tests

```csharp
// MyApp.Infrastructure.Tests/Storage/AzureBlobStorageIntegrationTests.cs
using Xunit;
using Orleans.Storage;
using Azure.Storage.Blobs;

namespace MyApp.Infrastructure.Tests.Storage
{
    /// <summary>
    /// Integration tests for Azure Blob Storage provider.
    /// These tests require Azure Storage Emulator (Azurite) or real Azure account.
    /// </summary>
    [Trait("Category", "Integration")]
    public class AzureBlobStorageIntegrationTests : IClassFixture<AzureStorageFixture>
    {
        private readonly IGrainStorage _storage;
        private readonly string _containerName;
        
        public AzureBlobStorageIntegrationTests(AzureStorageFixture fixture)
        {
            _storage = fixture.Storage;
            _containerName = fixture.ContainerName;
        }
        
        [SkippableFact]
        public async Task WriteAndReadState_WithAzureBlob_ShouldPersist()
        {
            Skip.IfNot(AzureStorageFixture.IsAvailable, "Azure Storage Emulator not available");
            
            // Arrange
            var grainId = GrainId.Create("order", Guid.NewGuid().ToString());
            var orderId = Guid.NewGuid();
            var grainState = new GrainState<OrderState>
            {
                State = new OrderState
                {
                    OrderId = orderId,
                    Status = OrderStatus.Placed,
                    CustomerId = "CUST-123"
                }
            };
            
            // Act
            await _storage.WriteStateAsync("OrderGrain", grainId, grainState);
            
            // Read back
            var readState = new GrainState<OrderState> { State = new OrderState() };
            await _storage.ReadStateAsync("OrderGrain", grainId, readState);
            
            // Assert
            Assert.Equal(orderId, readState.State.OrderId);
            Assert.Equal(OrderStatus.Placed, readState.State.Status);
        }
    }
    
    public class AzureStorageFixture : IDisposable
    {
        public IGrainStorage Storage { get; }
        public string ContainerName { get; }
        public static bool IsAvailable { get; private set; }
        
        public AzureStorageFixture()
        {
            ContainerName = $"test-{Guid.NewGuid():N}";
            
            try
            {
                var connectionString = "UseDevelopmentStorage=true"; // Azurite
                var options = new AzureBlobStorageOptions
                {
                    ContainerName = ContainerName
                };
                options.ConfigureBlobServiceClient(connectionString);
                
                Storage = new AzureBlobGrainStorage(
                    "test-storage",
                    options,
                    new JsonGrainStorageSerializer(),
                    NullLoggerFactory.Instance);
                
                IsAvailable = true;
            }
            catch
            {
                IsAvailable = false;
            }
        }
        
        public void Dispose()
        {
            if (IsAvailable)
            {
                // Cleanup container
                var connectionString = "UseDevelopmentStorage=true";
                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
                containerClient.DeleteIfExists();
            }
        }
    }
}
```

## Layer 4: Contract and Compatibility Tests

### Objectives

Verify that grain contracts remain stable across versions and that serialization works correctly for all message types.

### Contract Stability Tests

```csharp
// MyApp.GrainContracts.Tests/ContractStabilityTests.cs
using Xunit;
using System.Reflection;
using MyApp.GrainContracts;

namespace MyApp.GrainContracts.Tests
{
    public class ContractStabilityTests
    {
        [Fact]
        public void AllGrainInterfaces_ShouldInheritFromIGrain()
        {
            // Arrange
            var assembly = typeof(IOrderGrain).Assembly;
            var grainInterfaces = assembly.GetTypes()
                .Where(t => t.IsInterface && t.Name.StartsWith("I") && t.Name.EndsWith("Grain"));
            
            // Act & Assert
            foreach (var grainInterface in grainInterfaces)
            {
                Assert.True(
                    typeof(IGrain).IsAssignableFrom(grainInterface),
                    $"{grainInterface.Name} must inherit from IGrain or IGrainWithKey");
            }
        }
        
        [Fact]
        public void GrainMethods_ShouldReturnTaskOrValueTask()
        {
            // Arrange
            var assembly = typeof(IOrderGrain).Assembly;
            var grainInterfaces = assembly.GetTypes()
                .Where(t => t.IsInterface && typeof(IGrain).IsAssignableFrom(t));
            
            // Act & Assert
            foreach (var grainInterface in grainInterfaces)
            {
                foreach (var method in grainInterface.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    var returnType = method.ReturnType;
                    Assert.True(
                        returnType == typeof(Task) ||
                        returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>) ||
                        returnType == typeof(ValueTask) ||
                        returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>),
                        $"{grainInterface.Name}.{method.Name} must return Task or ValueTask");
                }
            }
        }
        
        [Fact]
        public void GrainMethods_ShouldNotHaveRefOrOutParameters()
        {
            // Arrange
            var assembly = typeof(IOrderGrain).Assembly;
            var grainInterfaces = assembly.GetTypes()
                .Where(t => t.IsInterface && typeof(IGrain).IsAssignableFrom(t));
            
            // Act & Assert
            foreach (var grainInterface in grainInterfaces)
            {
                foreach (var method in grainInterface.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    foreach (var parameter in method.GetParameters())
                    {
                        Assert.False(
                            parameter.IsOut || parameter.ParameterType.IsByRef,
                            $"{grainInterface.Name}.{method.Name} has ref/out parameter {parameter.Name}");
                    }
                }
            }
        }
    }
}
```

### Serialization Tests

```csharp
// MyApp.Contracts.Tests/SerializationTests.cs
using Xunit;
using Orleans.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Contracts.Tests
{
    public class SerializationTests
    {
        private readonly Serializer _serializer;
        
        public SerializationTests()
        {
            var services = new ServiceCollection();
            services.AddSerializer(builder =>
            {
                builder.AddAssembly(typeof(PlaceOrderRequest).Assembly);
            });
            
            var serviceProvider = services.BuildServiceProvider();
            _serializer = serviceProvider.GetRequiredService<Serializer>();
        }
        
        [Fact]
        public void PlaceOrderRequest_ShouldSerializeAndDeserialize()
        {
            // Arrange
            var original = new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 2,
                TotalAmount = 99.99m
            };
            
            // Act
            var serialized = _serializer.SerializeToArray(original);
            var deserialized = _serializer.Deserialize<PlaceOrderRequest>(serialized);
            
            // Assert
            Assert.Equal(original.CustomerId, deserialized.CustomerId);
            Assert.Equal(original.ProductId, deserialized.ProductId);
            Assert.Equal(original.Quantity, deserialized.Quantity);
            Assert.Equal(original.TotalAmount, deserialized.TotalAmount);
        }
        
        [Fact]
        public void PlaceOrderRequest_WithNullFields_ShouldSerialize()
        {
            // Arrange
            var original = new PlaceOrderRequest
            {
                CustomerId = null,
                ProductId = "PROD-456",
                Quantity = 1,
                TotalAmount = 50.00m
            };
            
            // Act
            var serialized = _serializer.SerializeToArray(original);
            var deserialized = _serializer.Deserialize<PlaceOrderRequest>(serialized);
            
            // Assert
            Assert.Null(deserialized.CustomerId);
            Assert.Equal(original.ProductId, deserialized.ProductId);
        }
        
        [Fact]
        public void OrderStateDto_WithComplexTypes_ShouldSerialize()
        {
            // Arrange
            var original = new OrderStateDto
            {
                OrderId = Guid.NewGuid(),
                Status = "Placed",
                CustomerId = "CUST-123",
                TotalAmount = 99.99m,
                Items = new List<OrderItemDto>
                {
                    new() { ProductId = "PROD-1", Quantity = 2, Price = 25.00m },
                    new() { ProductId = "PROD-2", Quantity = 1, Price = 49.99m }
                },
                Metadata = new Dictionary<string, string>
                {
                    ["Source"] = "WebApp",
                    ["Campaign"] = "Summer2024"
                }
            };
            
            // Act
            var serialized = _serializer.SerializeToArray(original);
            var deserialized = _serializer.Deserialize<OrderStateDto>(serialized);
            
            // Assert
            Assert.Equal(original.OrderId, deserialized.OrderId);
            Assert.Equal(original.Items.Count, deserialized.Items.Count);
            Assert.Equal(original.Metadata["Source"], deserialized.Metadata["Source"]);
        }
    }
}
```

### Backward Compatibility Tests

```csharp
// MyApp.Contracts.Tests/BackwardCompatibilityTests.cs
using Xunit;
using Orleans.Serialization;

namespace MyApp.Contracts.Tests
{
    public class BackwardCompatibilityTests
    {
        [Fact]
        public void PlaceOrderRequest_V2_ShouldDeserializeFromV1()
        {
            // Simulate V1 message (without new Priority field)
            var v1Data = new byte[] { /* serialized V1 data */ };
            
            // Act - Deserialize as V2
            var v2Request = _serializer.Deserialize<PlaceOrderRequestV2>(v1Data);
            
            // Assert - New field should have default value
            Assert.Equal(OrderPriority.Normal, v2Request.Priority);
        }
        
        [Fact]
        public void PlaceOrderRequestV1_ShouldDeserializeFromV2()
        {
            // Arrange - V2 message with new field
            var v2Request = new PlaceOrderRequestV2
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 1,
                TotalAmount = 50.00m,
                Priority = OrderPriority.High // New field
            };
            
            var v2Data = _serializer.SerializeToArray(v2Request);
            
            // Act - Deserialize as V1 (ignoring new field)
            var v1Request = _serializer.Deserialize<PlaceOrderRequest>(v2Data);
            
            // Assert - Should deserialize successfully
            Assert.Equal(v2Request.CustomerId, v1Request.CustomerId);
            Assert.Equal(v2Request.ProductId, v1Request.ProductId);
        }
    }
}
```

## Layer 5: End-to-End Tests (API → Grains → Storage → Streams)

### Objectives

Test complete user workflows from API endpoints through grains, storage, and streaming to verify system integration.

### E2E Test Setup

```csharp
// MyApp.Integration.Tests/TestWebApplicationFactory.cs
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.TestingHost;

namespace MyApp.Integration.Tests
{
    public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private TestCluster _cluster;
        
        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Setup test cluster
            var clusterBuilder = new TestClusterBuilder();
            clusterBuilder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
            _cluster = clusterBuilder.Build();
            _cluster.Deploy();
            
            // Configure test API to use test cluster
            builder.ConfigureServices(services =>
            {
                // Remove the real Orleans client registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IClusterClient));
                if (descriptor != null)
                    services.Remove(descriptor);
                
                // Add test cluster client
                services.AddSingleton(_cluster.Client);
                services.AddSingleton<IClusterClient>(_cluster.Client);
            });
            
            return base.CreateHost(builder);
        }
        
        public async Task InitializeAsync()
        {
            // Cluster is already deployed in CreateHost
            await Task.CompletedTask;
        }
        
        public new async Task DisposeAsync()
        {
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
            }
            await base.DisposeAsync();
        }
        
        private class TestSiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.AddMemoryGrainStorage("orders");
                siloBuilder.AddMemoryGrainStorage("inventory");
                siloBuilder.AddMemoryStreams<DefaultMemoryMessageBodySerializer>("orders");
                
                siloBuilder.ConfigureApplicationParts(parts =>
                    parts.AddApplicationPart(typeof(OrderGrain).Assembly)
                         .WithReferences());
                
                siloBuilder.ConfigureServices(services =>
                {
                    services.AddScoped<PlaceOrderUseCase>();
                    services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
                    services.AddScoped<IInventoryService, MockInventoryService>();
                    services.AddScoped<IPaymentService, MockPaymentService>();
                });
            }
        }
    }
}
```

### E2E Workflow Tests

```csharp
// MyApp.Integration.Tests/OrderWorkflowE2ETests.cs
using Xunit;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MyApp.Integration.Tests
{
    public class OrderWorkflowE2ETests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;
        
        public OrderWorkflowE2ETests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }
        
        [Fact]
        public async Task CompleteOrderWorkflow_ShouldSucceed()
        {
            // Step 1: Place order via API
            var placeOrderRequest = new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 2,
                TotalAmount = 99.99m
            };
            
            var placeResponse = await _client.PostAsJsonAsync("/api/orders", placeOrderRequest);
            placeResponse.EnsureSuccessStatusCode();
            
            var placeResult = await placeResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>();
            Assert.NotNull(placeResult);
            Assert.True(placeResult.Success);
            var orderId = placeResult.OrderId;
            
            // Step 2: Verify order status via API
            var statusResponse = await _client.GetAsync($"/api/orders/{orderId}");
            statusResponse.EnsureSuccessStatusCode();
            
            var orderState = await statusResponse.Content.ReadFromJsonAsync<OrderStateDto>();
            Assert.Equal("Placed", orderState.Status);
            Assert.Equal(placeOrderRequest.CustomerId, orderState.CustomerId);
            
            // Step 3: Verify inventory was reserved via API
            var inventoryResponse = await _client.GetAsync($"/api/inventory/{placeOrderRequest.ProductId}");
            inventoryResponse.EnsureSuccessStatusCode();
            
            var inventoryState = await inventoryResponse.Content.ReadFromJsonAsync<InventoryStateDto>();
            Assert.Contains(orderId, inventoryState.Reservations.Keys);
            
            // Step 4: Cancel order via API
            var cancelResponse = await _client.PostAsync($"/api/orders/{orderId}/cancel", null);
            cancelResponse.EnsureSuccessStatusCode();
            
            // Step 5: Verify order cancelled
            var finalStatusResponse = await _client.GetAsync($"/api/orders/{orderId}");
            var finalState = await finalStatusResponse.Content.ReadFromJsonAsync<OrderStateDto>();
            Assert.Equal("Cancelled", finalState.Status);
            
            // Step 6: Verify inventory released
            var finalInventoryResponse = await _client.GetAsync($"/api/inventory/{placeOrderRequest.ProductId}");
            var finalInventoryState = await finalInventoryResponse.Content.ReadFromJsonAsync<InventoryStateDto>();
            Assert.DoesNotContain(orderId, finalInventoryState.Reservations.Keys);
        }
        
        [Fact]
        public async Task PlaceOrder_WithInvalidData_ShouldReturnBadRequest()
        {
            // Arrange
            var invalidRequest = new PlaceOrderRequest
            {
                CustomerId = null, // Invalid: required field
                ProductId = "PROD-456",
                Quantity = 0, // Invalid: must be positive
                TotalAmount = -10.00m // Invalid: must be positive
            };
            
            // Act
            var response = await _client.PostAsJsonAsync("/api/orders", invalidRequest);
            
            // Assert
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }
        
        [Fact]
        public async Task PlaceOrder_WithUnavailableInventory_ShouldReturnConflict()
        {
            // Arrange - First drain all inventory
            var productId = "PROD-LIMITED";
            await DrainInventory(productId);
            
            var request = new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = productId,
                Quantity = 1,
                TotalAmount = 50.00m
            };
            
            // Act
            var response = await _client.PostAsJsonAsync("/api/orders", request);
            
            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
            
            var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.Contains("not available", errorResponse.Message);
        }
        
        private async Task DrainInventory(string productId)
        {
            // Helper method to drain inventory for testing
            await _client.PostAsync($"/api/inventory/{productId}/drain", null);
        }
    }
}
```

### Stream E2E Tests

```csharp
// MyApp.Integration.Tests/OrderStreamE2ETests.cs
using Xunit;
using Orleans;
using Orleans.Streams;

namespace MyApp.Integration.Tests
{
    public class OrderStreamE2ETests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly IClusterClient _client;
        
        public OrderStreamE2ETests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Services.GetRequiredService<IClusterClient>();
        }
        
        [Fact]
        public async Task PlaceOrder_ShouldPublishEventToStream()
        {
            // Arrange
            var streamProvider = _client.GetStreamProvider("orders");
            var stream = streamProvider.GetStream<OrderEvent>(
                StreamId.Create("order-events", "global"));
            
            var receivedEvents = new List<OrderEvent>();
            var tcs = new TaskCompletionSource<bool>();
            
            var subscription = await stream.SubscribeAsync(async (evt, token) =>
            {
                receivedEvents.Add(evt);
                if (evt.EventType == "OrderPlaced")
                {
                    tcs.SetResult(true);
                }
            });
            
            // Act - Place order through API
            var httpClient = _factory.CreateClient();
            var response = await httpClient.PostAsJsonAsync("/api/orders", new PlaceOrderRequest
            {
                CustomerId = "CUST-123",
                ProductId = "PROD-456",
                Quantity = 1,
                TotalAmount = 50.00m
            });
            
            // Wait for stream event
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            
            // Assert
            Assert.True(completed == tcs.Task, "Stream event not received within timeout");
            Assert.Single(receivedEvents);
            Assert.Equal("OrderPlaced", receivedEvents[0].EventType);
            
            // Cleanup
            await subscription.UnsubscribeAsync();
        }
    }
}
```

## Test Execution Strategy

### Local Development

```bash
# Run fast unit tests during development
dotnet test --filter "Category!=Integration&Category!=E2E" --logger "console;verbosity=minimal"

# Run all tests before committing
dotnet test --logger "console;verbosity=normal"
```

### CI/CD Pipeline

```yaml
# .github/workflows/test.yml
name: Test Suite

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Run Unit Tests
        run: dotnet test --filter "Category!=Integration&Category!=E2E" --logger trx --collect:"XPlat Code Coverage"
      
      - name: Upload Test Results
        uses: actions/upload-artifact@v3
        with:
          name: unit-test-results
          path: '**/TestResults/*.trx'
  
  integration-tests:
    runs-on: ubuntu-latest
    needs: unit-tests
    services:
      azurite:
        image: mcr.microsoft.com/azure-storage/azurite
        ports:
          - 10000:10000
          - 10001:10001
          - 10002:10002
    
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Run Integration Tests
        run: dotnet test --filter "Category=Integration" --logger trx
      
      - name: Upload Test Results
        uses: actions/upload-artifact@v3
        with:
          name: integration-test-results
          path: '**/TestResults/*.trx'
  
  e2e-tests:
    runs-on: ubuntu-latest
    needs: integration-tests
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Run E2E Tests
        run: dotnet test --filter "Category=E2E" --logger trx
      
      - name: Upload Test Results
        uses: actions/upload-artifact@v3
        with:
          name: e2e-test-results
          path: '**/TestResults/*.trx'
```

## Summary

This comprehensive testing strategy provides:

1. **Fast Feedback**: Unit tests run in <1 minute during development
2. **High Confidence**: Integration tests verify Orleans-specific behavior
3. **Realistic Validation**: E2E tests confirm complete user workflows
4. **Maintainability**: Clear separation of concerns across test layers
5. **CI/CD Ready**: Structured for automated pipeline execution

By following this strategy, teams can build robust Orleans applications with confidence that changes don't introduce regressions while maintaining rapid development velocity.
