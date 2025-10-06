# Modular Solution Structure for Orleans Applications

## Overview

This document proposes a comprehensive modular solution structure for Orleans-based applications, drawing on Clean Architecture, Hexagonal Architecture, and Domain-Driven Design principles. The structure supports both application-level modularization (organizing your grains, contracts, and hosting) and optional Orleans runtime refactoring (decomposing the monolithic `Orleans.Runtime` assembly).

The modular approach provides clear separation of concerns, testability, independent deployability, and easier onboarding for new team members. It establishes explicit dependency rules that prevent tight coupling and enable incremental evolution of both application and framework code.

## Architecture Layers

### Application-Level Layers

The following layers organize an Orleans-based application into cohesive, loosely coupled modules:

#### 1. Domain Layer

**Responsibility**: Pure business logic and domain entities with no dependencies on Orleans or infrastructure concerns.

**Contents**:
- Domain entities and value objects
- Domain services and business rules
- Domain events
- Aggregate roots
- Domain exceptions

**Dependencies**: None (or only primitive .NET libraries)

**Example**:
```csharp
// MyApp.Domain/Entities/Order.cs
public class Order
{
    public Guid OrderId { get; private set; }
    public string CustomerId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    
    public void PlaceOrder(string customerId, decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("Order amount must be positive");
            
        OrderId = Guid.NewGuid();
        CustomerId = customerId;
        TotalAmount = amount;
        Status = OrderStatus.Placed;
    }
    
    public void Cancel()
    {
        if (Status == OrderStatus.Shipped)
            throw new DomainException("Cannot cancel shipped orders");
            
        Status = OrderStatus.Cancelled;
    }
}

public enum OrderStatus { Placed, Confirmed, Shipped, Delivered, Cancelled }
```

#### 2. Application Layer

**Responsibility**: Application-specific orchestration logic, use cases, and command/query handlers. Coordinates domain logic without depending on Orleans-specific concerns.

**Contents**:
- Use case implementations / application services
- Command and query handlers
- DTOs for application boundaries
- Application-level validation
- Transaction coordination interfaces

**Dependencies**: Domain Layer only

**Example**:
```csharp
// MyApp.Application/UseCases/PlaceOrderUseCase.cs
public class PlaceOrderUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    
    public PlaceOrderUseCase(
        IOrderRepository orderRepository,
        IInventoryService inventoryService)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
    }
    
    public async Task<OrderResult> ExecuteAsync(PlaceOrderCommand command)
    {
        // Check inventory availability
        var available = await _inventoryService.CheckAvailabilityAsync(
            command.ProductId, command.Quantity);
        
        if (!available)
            return OrderResult.Failed("Product not available");
        
        // Create and place order
        var order = new Order();
        order.PlaceOrder(command.CustomerId, command.TotalAmount);
        
        // Persist
        await _orderRepository.SaveAsync(order);
        
        return OrderResult.Success(order.OrderId);
    }
}
```

#### 3. Contracts Layer

**Responsibility**: Shared types, interfaces, and DTOs that define communication boundaries between services and external clients.

**Contents**:
- Serializable DTOs
- Shared enums and constants
- Request/response models
- Service interfaces (for external APIs)

**Dependencies**: None (or minimal shared libraries)

**Example**:
```csharp
// MyApp.Contracts/Orders/PlaceOrderRequest.cs
[GenerateSerializer]
public record PlaceOrderRequest
{
    [Id(0)] public string CustomerId { get; init; }
    [Id(1)] public string ProductId { get; init; }
    [Id(2)] public int Quantity { get; init; }
    [Id(3)] public decimal TotalAmount { get; init; }
}

[GenerateSerializer]
public record PlaceOrderResponse
{
    [Id(0)] public Guid OrderId { get; init; }
    [Id(1)] public bool Success { get; init; }
    [Id(2)] public string Message { get; init; }
}
```

#### 4. GrainContracts Layer

**Responsibility**: Orleans grain interfaces that define the distributed object model and RPC contracts.

**Contents**:
- `IGrain` interfaces
- Grain-specific request/response types
- Observer interfaces
- Grain identity helpers

**Dependencies**: `Orleans.Core.Abstractions`, Contracts Layer (for DTOs)

**Example**:
```csharp
// MyApp.GrainContracts/IOrderGrain.cs
public interface IOrderGrain : IGrainWithGuidKey
{
    Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request);
    Task<OrderStateDto> GetOrderStatusAsync();
    Task CancelOrderAsync();
}

// MyApp.GrainContracts/IInventoryGrain.cs
public interface IInventoryGrain : IGrainWithStringKey
{
    Task<bool> CheckAvailabilityAsync(int quantity);
    Task ReserveStockAsync(Guid orderId, int quantity);
    Task ReleaseStockAsync(Guid orderId);
}
```

#### 5. Grains Layer

**Responsibility**: Implementation of grain contracts, coordinating application use cases and managing grain lifecycle.

**Contents**:
- Grain implementations
- Grain state classes
- Grain-level orchestration
- Integration with application layer use cases

**Dependencies**: `Orleans.Core`, `Orleans.Runtime`, GrainContracts, Application Layer

**Example**:
```csharp
// MyApp.Grains/OrderGrain.cs
public class OrderGrain : Grain, IOrderGrain
{
    private readonly IPersistentState<OrderState> _state;
    private readonly PlaceOrderUseCase _placeOrderUseCase;
    private readonly ILogger<OrderGrain> _logger;
    
    public OrderGrain(
        [PersistentState("order")] IPersistentState<OrderState> state,
        PlaceOrderUseCase placeOrderUseCase,
        ILogger<OrderGrain> logger)
    {
        _state = state;
        _placeOrderUseCase = placeOrderUseCase;
        _logger = logger;
    }
    
    public async Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request)
    {
        _logger.LogInformation("Placing order for customer {CustomerId}", request.CustomerId);
        
        // Delegate to application use case
        var command = new PlaceOrderCommand
        {
            CustomerId = request.CustomerId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            TotalAmount = request.TotalAmount
        };
        
        var result = await _placeOrderUseCase.ExecuteAsync(command);
        
        if (result.Success)
        {
            _state.State.OrderId = result.OrderId;
            _state.State.Status = OrderStatus.Placed;
            await _state.WriteStateAsync();
        }
        
        return new PlaceOrderResponse
        {
            OrderId = result.OrderId,
            Success = result.Success,
            Message = result.Message
        };
    }
    
    public Task<OrderStateDto> GetOrderStatusAsync()
    {
        return Task.FromResult(new OrderStateDto
        {
            OrderId = _state.State.OrderId,
            Status = _state.State.Status.ToString()
        });
    }
    
    public async Task CancelOrderAsync()
    {
        _state.State.Status = OrderStatus.Cancelled;
        await _state.WriteStateAsync();
    }
}

[GenerateSerializer]
public class OrderState
{
    [Id(0)] public Guid OrderId { get; set; }
    [Id(1)] public OrderStatus Status { get; set; }
}
```

#### 6. Infrastructure Layer

**Responsibility**: Concrete implementations of infrastructure concerns such as storage, messaging, external service integration, and cross-cutting concerns.

**Contents**:
- Storage provider implementations
- External API clients
- Repository implementations
- Message queue adapters
- Logging and telemetry infrastructure
- Configuration providers

**Dependencies**: Domain, Application, `Orleans.Core`, provider-specific packages

**Example**:
```csharp
// MyApp.Infrastructure/Repositories/OrderRepository.cs
public class OrderRepository : IOrderRepository
{
    private readonly IGrainFactory _grainFactory;
    
    public OrderRepository(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }
    
    public async Task<Order> GetByIdAsync(Guid orderId)
    {
        var grain = _grainFactory.GetGrain<IOrderGrain>(orderId);
        var state = await grain.GetOrderStatusAsync();
        
        // Map grain state to domain entity
        return MapToDomain(state);
    }
    
    public async Task SaveAsync(Order order)
    {
        var grain = _grainFactory.GetGrain<IOrderGrain>(order.OrderId);
        await grain.PlaceOrderAsync(new PlaceOrderRequest
        {
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount
        });
    }
    
    private Order MapToDomain(OrderStateDto state)
    {
        // Mapping logic here
        var order = new Order();
        // ... populate from state
        return order;
    }
}

// MyApp.Infrastructure/ExternalServices/PaymentServiceClient.cs
public class PaymentServiceClient : IPaymentService
{
    private readonly HttpClient _httpClient;
    
    public PaymentServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<PaymentResult> ProcessPaymentAsync(
        string customerId, decimal amount)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/payments",
            new { customerId, amount });
        
        return await response.Content.ReadFromJsonAsync<PaymentResult>();
    }
}
```

#### 7. SiloHost / Client Layer

**Responsibility**: Application entry points, hosting infrastructure, and Orleans configuration.

**Contents**:
- Silo host configuration and startup
- Client configuration
- DI container setup
- Middleware configuration
- Health checks

**Dependencies**: All application layers, Orleans hosting packages

**Example**:
```csharp
// MyApp.SiloHost/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure Orleans Silo
builder.Host.UseOrleans((context, siloBuilder) =>
{
    siloBuilder
        .UseLocalhostClustering()
        .ConfigureApplicationParts(parts => parts
            .AddApplicationPart(typeof(OrderGrain).Assembly)
            .WithReferences())
        .AddMemoryGrainStorage("orders")
        .UseDashboard(options =>
        {
            options.Port = 8080;
        });
});

// Register application services
builder.Services.AddScoped<PlaceOrderUseCase>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddHttpClient<IPaymentService, PaymentServiceClient>(client =>
{
    client.BaseAddress = new Uri("https://payment-api.example.com");
});

var app = builder.Build();

app.MapGet("/health", () => "Healthy");
app.Run();
```

#### 8. API Layer

**Responsibility**: Exposing grain functionality via HTTP APIs, GraphQL, gRPC, or other protocols.

**Contents**:
- REST API controllers
- GraphQL schemas and resolvers
- gRPC service implementations
- API models and validation
- Authentication/authorization

**Dependencies**: GrainContracts, Contracts, `Orleans.Core` (for IGrainFactory)

**Example**:
```csharp
// MyApp.API/Controllers/OrdersController.cs
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<OrdersController> _logger;
    
    public OrdersController(
        IGrainFactory grainFactory,
        ILogger<OrdersController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }
    
    [HttpPost]
    public async Task<ActionResult<PlaceOrderResponse>> PlaceOrder(
        [FromBody] PlaceOrderRequest request)
    {
        var orderId = Guid.NewGuid();
        var grain = _grainFactory.GetGrain<IOrderGrain>(orderId);
        
        var response = await grain.PlaceOrderAsync(request);
        
        if (response.Success)
            return CreatedAtAction(
                nameof(GetOrder),
                new { orderId = response.OrderId },
                response);
        
        return BadRequest(response);
    }
    
    [HttpGet("{orderId}")]
    public async Task<ActionResult<OrderStateDto>> GetOrder(Guid orderId)
    {
        var grain = _grainFactory.GetGrain<IOrderGrain>(orderId);
        var state = await grain.GetOrderStatusAsync();
        return Ok(state);
    }
    
    [HttpPost("{orderId}/cancel")]
    public async Task<ActionResult> CancelOrder(Guid orderId)
    {
        var grain = _grainFactory.GetGrain<IOrderGrain>(orderId);
        await grain.CancelOrderAsync();
        return NoContent();
    }
}
```

#### 9. Integration Workers Layer

**Responsibility**: Background workers, scheduled tasks, stream processors, and external event subscribers.

**Contents**:
- Background service implementations
- Stream subscriber grains
- Event processors
- Scheduled job handlers

**Dependencies**: GrainContracts, Application Layer, Infrastructure

**Example**:
```csharp
// MyApp.Workers/OrderStreamProcessor.cs
public class OrderStreamProcessor : BackgroundService
{
    private readonly IClusterClient _client;
    private readonly ILogger<OrderStreamProcessor> _logger;
    
    public OrderStreamProcessor(
        IClusterClient client,
        ILogger<OrderStreamProcessor> logger)
    {
        _client = client;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var streamProvider = _client.GetStreamProvider("orders");
        var stream = streamProvider.GetStream<OrderEvent>(
            StreamId.Create("order-events", "global"));
        
        await stream.SubscribeAsync(async (orderEvent, token) =>
        {
            _logger.LogInformation(
                "Processing order event: {EventType} for {OrderId}",
                orderEvent.EventType,
                orderEvent.OrderId);
            
            // Process event
            await ProcessOrderEventAsync(orderEvent);
        });
        
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
    
    private Task ProcessOrderEventAsync(OrderEvent orderEvent)
    {
        // Event processing logic
        return Task.CompletedTask;
    }
}
```

## Dependency Rules

The architecture enforces strict unidirectional dependencies:

```
API → GrainContracts → Contracts
  ↓                        ↑
Grains → Application → Domain
  ↓                        ↑
Infrastructure ←──────────┘
  ↓
SiloHost/Client (references all)
```

**Key Rules**:

1. **Domain Layer** has no dependencies on other layers or Orleans
2. **Application Layer** depends only on Domain
3. **GrainContracts** depends on Orleans abstractions and Contracts
4. **Grains** coordinate Application use cases but don't contain business logic
5. **Infrastructure** implements interfaces defined in Domain/Application
6. **API Layer** depends on GrainContracts for grain access
7. **SiloHost/Client** references all layers for composition

**Violations to Avoid**:
- Domain entities depending on Orleans types
- Application layer calling grains directly
- Grains implementing complex business logic
- Infrastructure details leaking into domain

## Example Project Tree

```
MyOrleansApp.sln
│
├── src/
│   ├── MyApp.Domain/
│   │   ├── Entities/
│   │   │   ├── Order.cs
│   │   │   └── Customer.cs
│   │   ├── ValueObjects/
│   │   │   └── Money.cs
│   │   ├── Services/
│   │   │   └── IPricingService.cs
│   │   └── Exceptions/
│   │       └── DomainException.cs
│   │
│   ├── MyApp.Application/
│   │   ├── UseCases/
│   │   │   ├── PlaceOrderUseCase.cs
│   │   │   └── CancelOrderUseCase.cs
│   │   ├── Commands/
│   │   │   └── PlaceOrderCommand.cs
│   │   ├── Queries/
│   │   │   └── GetOrderQuery.cs
│   │   └── Interfaces/
│   │       ├── IOrderRepository.cs
│   │       └── IPaymentService.cs
│   │
│   ├── MyApp.Contracts/
│   │   ├── Orders/
│   │   │   ├── PlaceOrderRequest.cs
│   │   │   ├── PlaceOrderResponse.cs
│   │   │   └── OrderStateDto.cs
│   │   └── Common/
│   │       └── Result.cs
│   │
│   ├── MyApp.GrainContracts/
│   │   ├── IOrderGrain.cs
│   │   ├── IInventoryGrain.cs
│   │   ├── ICustomerGrain.cs
│   │   └── Observers/
│   │       └── IOrderObserver.cs
│   │
│   ├── MyApp.Grains/
│   │   ├── OrderGrain.cs
│   │   ├── InventoryGrain.cs
│   │   ├── CustomerGrain.cs
│   │   └── States/
│   │       ├── OrderState.cs
│   │       └── InventoryState.cs
│   │
│   ├── MyApp.Infrastructure/
│   │   ├── Repositories/
│   │   │   └── OrderRepository.cs
│   │   ├── ExternalServices/
│   │   │   └── PaymentServiceClient.cs
│   │   ├── Storage/
│   │   │   └── CustomStorageProvider.cs
│   │   └── Messaging/
│   │       └── EventHubAdapter.cs
│   │
│   ├── MyApp.SiloHost/
│   │   ├── Program.cs
│   │   ├── Startup.cs
│   │   └── appsettings.json
│   │
│   ├── MyApp.API/
│   │   ├── Controllers/
│   │   │   ├── OrdersController.cs
│   │   │   └── CustomersController.cs
│   │   ├── Middleware/
│   │   │   └── ExceptionHandlingMiddleware.cs
│   │   └── Program.cs
│   │
│   └── MyApp.Workers/
│       ├── OrderStreamProcessor.cs
│       └── InventoryReconciliationWorker.cs
│
├── test/
│   ├── MyApp.Domain.Tests/
│   │   └── OrderTests.cs
│   ├── MyApp.Application.Tests/
│   │   └── PlaceOrderUseCaseTests.cs
│   ├── MyApp.Grains.Tests/
│   │   └── OrderGrainTests.cs
│   └── MyApp.Integration.Tests/
│       └── OrderFlowTests.cs
│
└── docs/
    ├── architecture.md
    └── deployment.md
```

## Dependency Injection Wiring

### Silo Configuration

```csharp
// MyApp.SiloHost/Program.cs
var builder = Host.CreateDefaultBuilder(args);

builder.UseOrleans((context, siloBuilder) =>
{
    // Clustering
    if (context.HostingEnvironment.IsDevelopment())
    {
        siloBuilder.UseLocalhostClustering();
    }
    else
    {
        siloBuilder.UseAzureStorageClustering(options =>
        {
            options.ConfigureTableServiceClient(
                context.Configuration["Orleans:Clustering:ConnectionString"]);
        });
    }
    
    // Grain storage
    siloBuilder
        .AddAzureBlobGrainStorage("orders", options =>
        {
            options.ConfigureBlobServiceClient(
                context.Configuration["Orleans:Storage:Orders:ConnectionString"]);
        })
        .AddMemoryGrainStorage("inventory");
    
    // Streaming
    siloBuilder.AddAzureTableGrainStorage("PubSubStore", options =>
    {
        options.ConfigureTableServiceClient(
            context.Configuration["Orleans:Streaming:ConnectionString"]);
    });
    
    siloBuilder.AddEventHubStreams("orders", configurator =>
    {
        configurator.ConfigureEventHub(builder => builder.Configure(options =>
        {
            options.ConfigureEventHubConnection(
                context.Configuration["Orleans:Streaming:EventHubConnectionString"],
                context.Configuration["Orleans:Streaming:EventHubName"],
                context.Configuration["Orleans:Streaming:EventHubConsumerGroup"]);
        }));
        
        configurator.UseAzureTableCheckpointer(builder => builder.Configure(options =>
        {
            options.ConfigureTableServiceClient(
                context.Configuration["Orleans:Streaming:ConnectionString"]);
        }));
    });
    
    // Application parts
    siloBuilder.ConfigureApplicationParts(parts => parts
        .AddApplicationPart(typeof(OrderGrain).Assembly)
        .WithReferences());
    
    // Grain call filters
    siloBuilder.AddIncomingGrainCallFilter<LoggingCallFilter>();
    siloBuilder.AddIncomingGrainCallFilter<ExceptionHandlingCallFilter>();
});

// Register application services
builder.ConfigureServices((context, services) =>
{
    // Application use cases
    services.AddScoped<PlaceOrderUseCase>();
    services.AddScoped<CancelOrderUseCase>();
    
    // Repositories
    services.AddScoped<IOrderRepository, OrderRepository>();
    
    // External services
    services.AddHttpClient<IPaymentService, PaymentServiceClient>(client =>
    {
        client.BaseAddress = new Uri(
            context.Configuration["ExternalServices:PaymentApi:BaseUrl"]);
    });
    
    // Background workers
    services.AddHostedService<OrderStreamProcessor>();
});

var app = builder.Build();
await app.RunAsync();
```

### Client Configuration

```csharp
// MyApp.API/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure Orleans Client
builder.Host.UseOrleansClient((context, clientBuilder) =>
{
    if (context.HostingEnvironment.IsDevelopment())
    {
        clientBuilder.UseLocalhostClustering();
    }
    else
    {
        clientBuilder.UseAzureStorageClustering(options =>
        {
            options.ConfigureTableServiceClient(
                context.Configuration["Orleans:Clustering:ConnectionString"]);
        });
    }
    
    clientBuilder.ConfigureApplicationParts(parts => parts
        .AddApplicationPart(typeof(IOrderGrain).Assembly));
});

// Register API services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<OrleansClientHealthCheck>("orleans-client");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();
```

### Test Configuration

```csharp
// MyApp.Grains.Tests/OrderGrainTests.cs
public class OrderGrainTests
{
    [Fact]
    public async Task PlaceOrder_ShouldStoreState()
    {
        // Arrange
        var cluster = new TestClusterBuilder()
            .AddSiloBuilderConfigurator<TestSiloConfigurator>()
            .Build();
        
        await cluster.DeployAsync();
        
        var grain = cluster.GrainFactory.GetGrain<IOrderGrain>(Guid.NewGuid());
        
        // Act
        var response = await grain.PlaceOrderAsync(new PlaceOrderRequest
        {
            CustomerId = "CUST-001",
            ProductId = "PROD-001",
            Quantity = 2,
            TotalAmount = 50.00m
        });
        
        // Assert
        Assert.True(response.Success);
        Assert.NotEqual(Guid.Empty, response.OrderId);
        
        var state = await grain.GetOrderStatusAsync();
        Assert.Equal("Placed", state.Status);
        
        await cluster.StopAllSilosAsync();
    }
}

public class TestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("orders");
        siloBuilder.ConfigureApplicationParts(parts => parts
            .AddApplicationPart(typeof(OrderGrain).Assembly)
            .WithReferences());
        
        // Mock dependencies
        siloBuilder.ConfigureServices(services =>
        {
            services.AddScoped<PlaceOrderUseCase>();
            services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
        });
    }
}
```

## NuGet Packaging Guidance

### Package Structure

For reusable components and internal libraries, organize NuGet packages by layer:

```
MyOrleansApp.Domain.nupkg
  - MyApp.Domain.dll

MyOrleansApp.Application.nupkg
  - MyApp.Application.dll
  - Dependencies: MyOrleansApp.Domain

MyOrleansApp.Contracts.nupkg
  - MyApp.Contracts.dll
  - Dependencies: Orleans.Core.Abstractions

MyOrleansApp.GrainContracts.nupkg
  - MyApp.GrainContracts.dll
  - Dependencies: Orleans.Core.Abstractions, MyOrleansApp.Contracts

MyOrleansApp.Grains.nupkg
  - MyApp.Grains.dll
  - Dependencies: Orleans.Runtime, MyOrleansApp.GrainContracts, MyOrleansApp.Application

MyOrleansApp.Infrastructure.nupkg
  - MyApp.Infrastructure.dll
  - Dependencies: MyOrleansApp.Application, Orleans.Core, provider packages

MyOrleansApp.Client.nupkg
  - MyApp.Client.dll (client utilities and helpers)
  - Dependencies: Orleans.Core, MyOrleansApp.GrainContracts
```

### Package Configuration

```xml
<!-- MyApp.GrainContracts/MyApp.GrainContracts.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <PackageId>MyOrleansApp.GrainContracts</PackageId>
    <Version>1.0.0</Version>
    <Authors>My Company</Authors>
    <Description>Grain contracts for MyOrleansApp</Description>
    <PackageTags>Orleans;Grains;Contracts</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Orleans.Core.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Orleans.Sdk" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyApp.Contracts\MyApp.Contracts.csproj" />
  </ItemGroup>
</Project>
```

### Versioning Strategy

Follow semantic versioning (SemVer) for internal packages:

- **Major version**: Breaking changes to grain contracts or domain models
- **Minor version**: New features, backward-compatible additions
- **Patch version**: Bug fixes, performance improvements

Maintain compatibility matrices:

```
Orleans Version    | GrainContracts | Grains  | Client
-------------------|----------------|---------|--------
8.0.x              | 1.0.x          | 1.0.x   | 1.0.x
8.1.x              | 1.1.x          | 1.1.x   | 1.1.x
9.0.x              | 2.0.x          | 2.0.x   | 2.0.x
```

## Configuration Boundaries

### Environment-Specific Configuration

Use configuration sections to isolate environment-specific settings:

```json
// appsettings.Production.json
{
  "Orleans": {
    "Clustering": {
      "ProviderType": "AzureStorage",
      "ConnectionString": "UseDevelopmentStorage=false;..."
    },
    "GrainStorage": {
      "orders": {
        "ProviderType": "AzureBlobStorage",
        "ConnectionString": "..."
      },
      "inventory": {
        "ProviderType": "AdoNet",
        "ConnectionString": "...",
        "Invariant": "System.Data.SqlClient"
      }
    },
    "Streaming": {
      "orders": {
        "ProviderType": "EventHub",
        "ConnectionString": "...",
        "EventHubName": "orders",
        "ConsumerGroup": "$Default"
      }
    }
  },
  "ExternalServices": {
    "PaymentApi": {
      "BaseUrl": "https://payment-api.example.com"
    }
  }
}
```

### Feature Flags

Use feature flags for gradual rollouts:

```csharp
builder.Services.AddFeatureManagement();

// In grain or service
public class OrderGrain : Grain, IOrderGrain
{
    private readonly IFeatureManager _featureManager;
    
    public async Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request)
    {
        if (await _featureManager.IsEnabledAsync("NewPricingEngine"))
        {
            // Use new pricing logic
        }
        else
        {
            // Use legacy pricing
        }
    }
}
```

## Optional Orleans Runtime Modularization

For teams working on Orleans framework itself, the monolithic `Orleans.Runtime` can be decomposed into focused assemblies:

### Proposed Runtime Modules

```
Orleans.Runtime.Activation/
  - Catalog.cs
  - ActivationDirectory.cs
  - ActivationCollector.cs
  - GrainContextActivator.cs

Orleans.Runtime.GrainDirectory/
  - LocalGrainDirectory.cs
  - GrainDirectoryPartition.cs
  - GrainLocator.cs
  - DhtGrainLocator.cs

Orleans.Runtime.Membership/
  - MembershipTableManager.cs
  - SiloStatusOracle.cs
  - MembershipGossiper.cs
  - ClusterHealthMonitor.cs

Orleans.Runtime.Messaging/
  - MessageCenter.cs
  - OutboundMessageQueue.cs
  - InboundMessageQueue.cs
  - MessageSerializer.cs

Orleans.Runtime.Placement/
  - PlacementService.cs
  - PlacementDirectorResolver.cs
  - ActivationRepartitioner.cs

Orleans.Runtime.Scheduling/
  - ActivationTaskScheduler.cs
  - WorkItemGroup.cs
  - OrleansTaskScheduler.cs
```

Each module would have clear dependencies and public APIs, enabling independent evolution and testing.

## Summary

This modular solution structure provides a robust foundation for Orleans-based applications. By separating concerns across well-defined layers with explicit dependency rules, teams can:

- Build testable, maintainable applications
- Scale development across multiple teams
- Evolve components independently
- Onboard new developers more easily
- Deploy selectively with confidence

The structure supports both greenfield projects and incremental modernization of existing codebases. Start with the layers most relevant to your application's needs, and expand the structure as complexity grows.
