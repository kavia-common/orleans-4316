# Decoupling Tightly Coupled Areas and Refactoring Strategies

## Overview

This document provides detailed strategies for decoupling tightly coupled areas identified in the architecture audit. Each section addresses a specific subsystem, analyzes the current coupling issues, and proposes concrete refactoring steps.

## Catalog Coupling

### Problem Analysis

The `Catalog` class (414 lines in `src/Orleans.Runtime/Catalog/Catalog.cs`) mixes multiple responsibilities:

- **Activation lifecycle**: Create, activate, rehydrate, deactivate operations.
- **Directory interaction**: Registration/unregistration with grain directory services.
- **Silo topology awareness**: Responding to silo status changes and determining which activations to deactivate.
- **Activation collection**: Coordinating with `ActivationCollector` for idle activation cleanup.

This mixing makes the Catalog difficult to:
- Test in isolation (requires mocking 8+ dependencies).
- Evolve activation lifecycle logic without affecting directory concerns.
- Reuse activation management logic in different contexts (e.g., client-side grain references).

### Refactoring Strategy

#### Step 1: Extract ActivationLifecycleManager

Create a focused service responsible only for activation lifecycle operations:

```csharp
// Orleans.Runtime.Activation/ActivationLifecycleManager.cs
internal sealed class ActivationLifecycleManager
{
    private readonly GrainContextActivator _grainActivator;
    private readonly ActivationDirectory _activations;
    private readonly ISiloStatusOracle _siloStatusOracle;
    private readonly ILogger _logger;

    public IGrainContext CreateActivation(
        GrainId grainId,
        Dictionary<string, object> requestContextData,
        MigrationContext rehydrationContext)
    {
        // Extracted from Catalog.GetOrCreateActivation
        // Pure lifecycle logic: create, rehydrate, activate
    }

    public Task DeactivateActivations(
        DeactivationReason reason,
        List<IGrainContext> activations,
        CancellationToken cancellationToken)
    {
        // Extracted from Catalog.DeactivateActivations
        // Pure deactivation orchestration
    }
}
```

#### Step 2: Introduce GrainDirectoryClient Abstraction

Abstract directory registration/unregistration behind a focused interface:

```csharp
// Orleans.Runtime.GrainDirectory/IGrainDirectoryClient.cs
public interface IGrainDirectoryClient
{
    Task<GrainAddress> Register(
        GrainAddress address,
        int hopCount = 0);

    Task Unregister(
        GrainAddress address,
        UnregistrationCause cause);

    ValueTask<GrainAddress> Lookup(
        GrainId grainId);

    void InvalidateCache(GrainId grainId);
}

// Implementation wraps GrainLocator and provides clean API
internal sealed class GrainDirectoryClient : IGrainDirectoryClient
{
    private readonly GrainLocator _grainLocator;
    // Implementation delegates to GrainLocator
}
```

#### Step 3: Create ActivationCoordinator

Introduce a coordinator that orchestrates lifecycle and directory operations:

```csharp
// Orleans.Runtime.Catalog/ActivationCoordinator.cs
internal sealed class ActivationCoordinator
{
    private readonly ActivationLifecycleManager _lifecycleManager;
    private readonly IGrainDirectoryClient _directoryClient;
    private readonly ActivationDirectory _activations;
    private readonly ILogger _logger;

    public async Task<IGrainContext> GetOrCreateActivation(
        GrainId grainId,
        Dictionary<string, object> requestContextData,
        MigrationContext rehydrationContext)
    {
        if (_activations.FindTarget(grainId) is { } existing)
        {
            return existing;
        }

        var activation = _lifecycleManager.CreateActivation(
            grainId, requestContextData, rehydrationContext);

        // Optionally register with directory (depends on placement strategy)
        await RegisterWithDirectoryIfNeeded(activation);

        return activation;
    }

    private async Task RegisterWithDirectoryIfNeeded(IGrainContext activation)
    {
        var placementStrategy = activation.GetComponent<PlacementStrategy>();
        if (placementStrategy?.IsUsingGrainDirectory == true)
        {
            await _directoryClient.Register(activation.Address);
        }
    }
}
```

#### Step 4: Extract SiloTopologyObserver

Move silo status change handling to a dedicated observer:

```csharp
// Orleans.Runtime.Catalog/SiloTopologyObserver.cs
internal sealed class SiloTopologyObserver : ILifecycleParticipant<ISiloLifecycle>
{
    private readonly ActivationDirectory _activations;
    private readonly ActivationLifecycleManager _lifecycleManager;
    private readonly GrainDirectoryResolver _directoryResolver;
    private readonly ILocalGrainDirectory _localDirectory;

    public void OnSiloStatusChange(
        SiloAddress updatedSilo,
        SiloStatus status)
    {
        if (!status.IsTerminating()) return;

        var affectedActivations = FindActivationsAffectedBySiloFailure(
            updatedSilo);

        if (affectedActivations.Count > 0)
        {
            var reason = new DeactivationReason(
                DeactivationReasonCode.DirectoryFailure,
                $"Silo {updatedSilo} failed and owned directory partitions");

            _lifecycleManager.DeactivateActivations(
                reason, affectedActivations, CancellationToken.None);
        }
    }

    private List<IGrainContext> FindActivationsAffectedBySiloFailure(
        SiloAddress failedSilo)
    {
        // Logic extracted from Catalog.OnSiloStatusChange
    }
}
```

### Target Module Structure

After refactoring:
- `Orleans.Runtime.Activation/ActivationLifecycleManager.cs`: Pure lifecycle operations.
- `Orleans.Runtime.Activation/ActivationCoordinator.cs`: Orchestration of lifecycle + directory.
- `Orleans.Runtime.GrainDirectory/IGrainDirectoryClient.cs`: Clean directory API.
- `Orleans.Runtime.GrainDirectory/GrainDirectoryClient.cs`: Implementation wrapping GrainLocator.
- `Orleans.Runtime.Catalog/SiloTopologyObserver.cs`: Silo failure response logic.

## MembershipTableManager Responsibilities

### Problem Analysis

The `MembershipTableManager` (1,182 lines) violates Single Responsibility Principle by combining:

1. **Table I/O**: Reading/writing membership table via `IMembershipTable`.
2. **Local Silo Lifecycle**: Managing the local silo's status transitions.
3. **Gossip Coordination**: Propagating updates via `IMembershipGossiper`.
4. **Suspect/Kill State Machine**: Implementing failure detection voting protocol.
5. **Periodic Refresh**: Polling membership table on a timer.
6. **Cleanup Logic**: Detecting and resolving temporal paradoxes.

### Refactoring Strategy

#### Step 1: Extract MembershipTableAccessor

Create a focused service for table I/O with retry logic:

```csharp
// Orleans.Runtime.MembershipService/MembershipTableAccessor.cs
internal sealed class MembershipTableAccessor
{
    private readonly IMembershipTable _table;
    private readonly ILogger _logger;

    public Task<MembershipTableData> ReadAll()
    {
        return ExecuteWithRetries(
            () => _table.ReadAll(),
            "ReadAll");
    }

    public Task<bool> InsertRow(
        MembershipEntry entry,
        TableVersion tableVersion)
    {
        return ExecuteWithRetries(
            () => _table.InsertRow(entry, tableVersion),
            "InsertRow");
    }

    public Task<bool> UpdateRow(
        MembershipEntry entry,
        string etag,
        TableVersion tableVersion)
    {
        return ExecuteWithRetries(
            () => _table.UpdateRow(entry, etag, tableVersion),
            "UpdateRow");
    }

    public Task UpdateIAmAlive(MembershipEntry entry)
    {
        return ExecuteWithRetries(
            () => _table.UpdateIAmAlive(entry),
            "UpdateIAmAlive");
    }

    private async Task<T> ExecuteWithRetries<T>(
        Func<Task<T>> operation,
        string operationName)
    {
        // Centralized retry logic with exponential backoff
        // Extracted from MembershipExecuteWithRetries
    }
}
```

#### Step 2: Extract MembershipGossipService

Create a service dedicated to gossip coordination:

```csharp
// Orleans.Runtime.MembershipService/MembershipGossipService.cs
internal sealed class MembershipGossipService
{
    private readonly IMembershipGossiper _gossiper;
    private readonly ClusterMembershipOptions _options;
    private readonly Func<DateTime> _getUtcNow;
    private readonly ILogger _logger;

    public async Task GossipToOthers(
        MembershipTableSnapshot snapshot,
        SiloAddress updatedSilo,
        SiloStatus updatedStatus)
    {
        if (!_options.UseLivenessGossip) return;

        var gossipPartners = SelectGossipPartners(snapshot);
        
        try
        {
            await _gossiper.GossipToRemoteSilos(
                gossipPartners,
                snapshot,
                updatedSilo,
                updatedStatus);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error gossiping status");
        }
    }

    private List<SiloAddress> SelectGossipPartners(
        MembershipTableSnapshot snapshot)
    {
        // Logic extracted from MembershipTableManager.GossipToOthers
        // Select active, non-stale silos
    }
}
```

#### Step 3: Extract SuspectOrKillService

Create a state machine service for failure detection:

```csharp
// Orleans.Runtime.MembershipService/SuspectOrKillService.cs
internal sealed class SuspectOrKillService
{
    private readonly MembershipTableAccessor _tableAccessor;
    private readonly MembershipGossipService _gossiper;
    private readonly ClusterMembershipOptions _options;
    private readonly ILogger _logger;

    public async Task<bool> TryToSuspectOrKill(
        SiloAddress silo,
        SiloAddress indirectProbingSilo = null)
    {
        var table = await _tableAccessor.ReadAll();
        var (entry, etag) = GetEntryAndEtag(table, silo);

        // Check if already dead
        if (entry.Status == SiloStatus.Dead) return true;

        // Get fresh votes
        var freshVotes = entry.GetFreshVotes(
            DateTime.UtcNow,
            _options.DeathVoteExpirationTimeout);

        // Add our vote
        entry.AddOrUpdateSuspector(
            _localSilo,
            DateTime.UtcNow,
            _options.NumVotesForDeathDeclaration);

        // Check if sufficient votes to kill
        if (HasSufficientVotesToKill(table, entry, freshVotes))
        {
            return await DeclareDead(entry, etag, table.Version);
        }

        // Update with new vote
        var ok = await _tableAccessor.UpdateRow(
            entry, etag, table.Version.Next());

        if (ok)
        {
            await _gossiper.GossipToOthers(
                snapshot, entry.SiloAddress, entry.Status);
        }

        return ok;
    }

    private bool HasSufficientVotesToKill(
        MembershipTableData table,
        MembershipEntry entry,
        IList<Tuple<SiloAddress, DateTime>> freshVotes)
    {
        // Logic extracted from InnerTryToSuspectOrKill
    }

    private async Task<bool> DeclareDead(
        MembershipEntry entry,
        string etag,
        TableVersion version)
    {
        // Logic extracted from DeclareDead
    }
}
```

#### Step 4: Extract LocalSiloLifecycleManager

Manage local silo status transitions:

```csharp
// Orleans.Runtime.MembershipService/LocalSiloLifecycleManager.cs
internal sealed class LocalSiloLifecycleManager
{
    private readonly MembershipTableAccessor _tableAccessor;
    private readonly MembershipGossipService _gossiper;
    private readonly ILocalSiloDetails _localSiloDetails;
    private readonly ILogger _logger;

    public SiloStatus CurrentStatus { get; private set; } = SiloStatus.Created;

    public async Task UpdateStatus(SiloStatus newStatus)
    {
        bool ok = await TryUpdateStatusWithRetries(newStatus);
        
        if (ok)
        {
            CurrentStatus = newStatus;
            await _gossiper.GossipToOthers(
                GetCurrentSnapshot(),
                _localSiloDetails.SiloAddress,
                newStatus);
        }
        else
        {
            throw new OrleansException(
                $"Failed to update status to {newStatus}");
        }
    }

    private async Task<bool> TryUpdateStatusWithRetries(
        SiloStatus newStatus)
    {
        // Retry logic extracted from UpdateStatus
    }

    public async Task UpdateIAmAlive()
    {
        var entry = new MembershipEntry
        {
            SiloAddress = _localSiloDetails.SiloAddress,
            IAmAliveTime = DateTime.UtcNow
        };

        await _tableAccessor.UpdateIAmAlive(entry);
    }
}
```

#### Step 5: Create MembershipOrchestrator

Thin coordinator that delegates to extracted services:

```csharp
// Orleans.Runtime.MembershipService/MembershipOrchestrator.cs
internal sealed class MembershipOrchestrator : ILifecycleParticipant<ISiloLifecycle>
{
    private readonly MembershipTableAccessor _tableAccessor;
    private readonly LocalSiloLifecycleManager _localLifecycle;
    private readonly MembershipGossipService _gossiper;
    private readonly SuspectOrKillService _suspectOrKillService;
    private readonly IAsyncTimer _refreshTimer;

    public Task UpdateStatus(SiloStatus status) =>
        _localLifecycle.UpdateStatus(status);

    public Task<bool> TryToSuspectOrKill(
        SiloAddress silo,
        SiloAddress indirectProbe = null) =>
        _suspectOrKillService.TryToSuspectOrKill(silo, indirectProbe);

    public async Task Refresh()
    {
        var table = await _tableAccessor.ReadAll();
        ProcessTableUpdate(table);
    }

    private void ProcessTableUpdate(MembershipTableData table)
    {
        // Publish snapshot to subscribers
        // Check for local silo being marked dead
        // Delegate to cleanup logic if needed
    }

    void ILifecycleParticipant<ISiloLifecycle>.Participate(
        ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe(
            nameof(MembershipOrchestrator),
            ServiceLifecycleStage.RuntimeGrainServices,
            OnStart,
            OnStop);
    }
}
```

### Target Module Structure

After refactoring:
- `Orleans.Runtime.Membership/MembershipTableAccessor.cs`: Table I/O with retries.
- `Orleans.Runtime.Membership/LocalSiloLifecycleManager.cs`: Local status management.
- `Orleans.Runtime.Membership/MembershipGossipService.cs`: Gossip coordination.
- `Orleans.Runtime.Membership/SuspectOrKillService.cs`: Failure detection state machine.
- `Orleans.Runtime.Membership/MembershipOrchestrator.cs`: Thin coordinator.

## GrainDirectoryPartition Complexity

### Problem Analysis

The `GrainDirectoryPartition` (920 lines) embeds complex protocols:

- **Range lock management**: Tracking locks during range transfers (`_rangeLocks`).
- **Snapshot transfer**: Pull-based protocol for transferring grain addresses.
- **Recovery**: Querying all cluster members for registered activations.
- **Membership view synchronization**: Processing membership changes and adjusting ranges.

### Refactoring Strategy

#### Step 1: Extract RangeLockManager

```csharp
// Orleans.Runtime.GrainDirectory/RangeLockManager.cs
internal sealed class RangeLockManager
{
    private readonly List<(RingRange Range, MembershipVersion Version, TaskCompletionSource Completion)> _locks = [];

    public (TaskCompletionSource Lock, ValueStopwatch Stopwatch) AcquireLock(
        RingRange range,
        MembershipVersion version)
    {
        var tcs = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _locks.Add((range, version, tcs));
        return (tcs, ValueStopwatch.StartNew());
    }

    public void ReleaseLock(
        RingRange range,
        MembershipVersion version,
        TaskCompletionSource tcs,
        bool cancelled)
    {
        if (cancelled)
        {
            tcs.SetCanceled();
        }
        else
        {
            tcs.SetResult();
            _locks.Remove((range, version, tcs));
        }
    }

    public async ValueTask WaitForRange(
        RingRange range,
        MembershipVersion version,
        CancellationToken cancellationToken)
    {
        while (TryGetIntersectingLock(range, version, out var task))
        {
            await task.WaitAsync(cancellationToken);
        }
    }

    private bool TryGetIntersectingLock(
        RingRange range,
        MembershipVersion version,
        out Task task)
    {
        foreach (var (lockRange, lockVersion, completion) in _locks)
        {
            if (lockVersion <= version && range.Intersects(lockRange))
            {
                task = completion.Task;
                return true;
            }
        }
        task = null;
        return false;
    }
}
```

#### Step 2: Extract SnapshotTransferService

```csharp
// Orleans.Runtime.GrainDirectory/SnapshotTransferService.cs
internal sealed class SnapshotTransferService
{
    private readonly IInternalGrainFactory _grainFactory;
    private readonly ILogger _logger;

    public async Task<bool> TransferSnapshotFromPreviousOwner(
        DirectoryMembershipSnapshot currentView,
        RingRange range,
        SiloAddress previousOwner,
        int partitionIndex,
        MembershipVersion previousVersion,
        Action<GrainAddress> onEntryReceived,
        CancellationToken cancellationToken)
    {
        try
        {
            var partition = GetPartitionReference(previousOwner, partitionIndex);
            var snapshot = await partition.GetSnapshotAsync(
                currentView.Version,
                previousVersion,
                range).AsTask().WaitAsync(cancellationToken);

            if (snapshot is null) return false;

            // Acknowledge receipt
            _ = partition.AcknowledgeSnapshotTransferAsync(
                _localSilo, _partitionIndex, previousVersion);

            // Process entries
            foreach (var entry in snapshot.GrainAddresses)
            {
                onEntryReceived(entry);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error transferring snapshot");
            return false;
        }
    }
}
```

#### Step 3: Extract RangeRecoveryService

```csharp
// Orleans.Runtime.GrainDirectory/RangeRecoveryService.cs
internal sealed class RangeRecoveryService
{
    private readonly IInternalGrainFactory _grainFactory;
    private readonly ILogger _logger;

    public async IAsyncEnumerable<List<GrainAddress>> RecoverRange(
        DirectoryMembershipSnapshot view,
        RingRange range,
        ClusterMembershipSnapshot clusterSnapshot,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tasks = new List<Task<List<GrainAddress>>>();

        foreach (var member in clusterSnapshot.Members.Values)
        {
            if (!IsActiveMember(member.Status)) continue;

            tasks.Add(GetRegisteredActivationsFromSilo(
                view.Version, range, member.SiloAddress, cancellationToken));
        }

        await Task.WhenAll(tasks).WaitAsync(cancellationToken);

        foreach (var task in tasks)
        {
            yield return await task;
        }
    }

    private async Task<List<GrainAddress>> GetRegisteredActivationsFromSilo(
        MembershipVersion version,
        RingRange range,
        SiloAddress silo,
        CancellationToken cancellationToken)
    {
        var client = _grainFactory.GetSystemTarget<IGrainDirectoryClient>(
            Constants.GrainDirectoryType, silo);

        return await client.RecoverRegisteredActivations(
            version, range, _localSilo, _partitionIndex);
    }

    private bool IsActiveMember(SiloStatus status) =>
        status is SiloStatus.Active or SiloStatus.Joining or SiloStatus.ShuttingDown;
}
```

#### Step 4: Simplify GrainDirectoryPartition

The partition becomes an orchestrator delegating to extracted services:

```csharp
// Orleans.Runtime.GrainDirectory/GrainDirectoryPartition.cs (simplified)
internal sealed partial class GrainDirectoryPartition : SystemTarget
{
    private readonly Dictionary<GrainId, GrainAddress> _directory = [];
    private readonly RangeLockManager _rangeLockManager;
    private readonly SnapshotTransferService _snapshotService;
    private readonly RangeRecoveryService _recoveryService;
    private RingRange _currentRange;

    private async Task AcquireRangeAsync(
        DirectoryMembershipSnapshot previous,
        DirectoryMembershipSnapshot current,
        RingRange addedRange)
    {
        var (lock, sw) = _rangeLockManager.AcquireLock(addedRange, current.Version);

        try
        {
            bool success = await TransferRangeFromPreviousOwners(
                previous, current, addedRange);

            if (!success)
            {
                await RecoverRange(current, addedRange);
            }
        }
        finally
        {
            _rangeLockManager.ReleaseLock(
                addedRange, current.Version, lock, ShutdownToken.IsCancellationRequested);
        }
    }

    private async Task<bool> TransferRangeFromPreviousOwners(
        DirectoryMembershipSnapshot previous,
        DirectoryMembershipSnapshot current,
        RingRange addedRange)
    {
        // Delegate to SnapshotTransferService
    }

    private async Task RecoverRange(
        DirectoryMembershipSnapshot current,
        RingRange addedRange)
    {
        await foreach (var activations in _recoveryService.RecoverRange(
            current, addedRange, _owner.ClusterMembershipSnapshot, ShutdownToken))
        {
            foreach (var entry in activations)
            {
                _directory[entry.GrainId] = entry;
            }
        }
    }
}
```

### Target Module Structure

After refactoring:
- `Orleans.Runtime.GrainDirectory/RangeLockManager.cs`: Lock management.
- `Orleans.Runtime.GrainDirectory/SnapshotTransferService.cs`: Snapshot transfer protocol.
- `Orleans.Runtime.GrainDirectory/RangeRecoveryService.cs`: Recovery from cluster members.
- `Orleans.Runtime.GrainDirectory/GrainDirectoryPartition.cs`: Orchestrator (reduced to ~400 lines).

## Streaming Runtime Coupling

### Problem Analysis

Stream providers are tightly coupled to silo internals via `IStreamProviderRuntime`, which exposes:
- `IServiceProvider` (full DI container access)
- `IInternalGrainFactory` (internal grain creation)
- `ILoggerFactory` (logging infrastructure)
- `IStreamPubSub` (pub/sub coordination)

This coupling makes it difficult to:
- Test stream providers independently.
- Host stream providers outside of Orleans silos.
- Evolve provider interfaces without breaking all implementations.

### Refactoring Strategy

#### Step 1: Introduce IStreamProviderConfigurationService

```csharp
// Orleans.Streaming.Abstractions/IStreamProviderConfigurationService.cs
public interface IStreamProviderConfigurationService
{
    TOptions GetOptions<TOptions>(string providerName) where TOptions : class;
    ILogger CreateLogger(string categoryName);
    IServiceProvider ServiceProvider { get; }
}
```

#### Step 2: Introduce IStreamProviderFactory

```csharp
// Orleans.Streaming.Abstractions/IStreamProviderFactory.cs
public interface IStreamProviderFactory
{
    IStreamProvider CreateProvider(
        string providerName,
        IStreamProviderConfigurationService configService);
}

// Example implementation
public class MemoryStreamProviderFactory : IStreamProviderFactory
{
    public IStreamProvider CreateProvider(
        string providerName,
        IStreamProviderConfigurationService configService)
    {
        var options = configService.GetOptions<MemoryStreamOptions>(providerName);
        var logger = configService.CreateLogger<MemoryStreamProvider>();
        
        return new MemoryStreamProvider(providerName, options, logger);
    }
}
```

#### Step 3: Create StreamProviderRuntimeAdapter

Adapt `IStreamProviderRuntime` to the new abstraction:

```csharp
// Orleans.Streaming/Internal/StreamProviderRuntimeAdapter.cs
internal sealed class StreamProviderRuntimeAdapter : IStreamProviderConfigurationService
{
    private readonly IStreamProviderRuntime _runtime;

    public TOptions GetOptions<TOptions>(string providerName) where TOptions : class
    {
        return _runtime.ServiceProvider
            .GetOptionsByName<TOptions>(providerName);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _runtime.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(categoryName);
    }

    public IServiceProvider ServiceProvider => _runtime.ServiceProvider;
}
```

#### Step 4: Update Provider Registration

```csharp
// Provider registration becomes:
services.AddSingleton<IStreamProviderFactory, MemoryStreamProviderFactory>();

// At runtime:
var factory = serviceProvider.GetRequiredService<IStreamProviderFactory>();
var configService = new StreamProviderRuntimeAdapter(runtime);
var provider = factory.CreateProvider("MyProvider", configService);
```

### Target Module Structure

- `Orleans.Streaming.Abstractions/IStreamProviderFactory.cs`
- `Orleans.Streaming.Abstractions/IStreamProviderConfigurationService.cs`
- `Orleans.Streaming/Internal/StreamProviderRuntimeAdapter.cs`
- Provider implementations depend only on abstractions

## Storage and Serialization Conflation

### Problem Analysis

Some storage providers handle serialization inline rather than delegating to `IGrainStorageSerializer`, creating inconsistent patterns and making it difficult to:
- Swap serializers without modifying storage providers.
- Test serialization logic independently from storage I/O.
- Apply serialization optimizations globally.

### Refactoring Strategy

#### Step 1: Introduce IStorageAdapter<TState>

```csharp
// Orleans.Core/Storage/IStorageAdapter.cs
public interface IStorageAdapter<TState>
{
    Task<TState> ReadStateAsync(
        string grainType,
        GrainId grainId,
        IGrainState<TState> grainState);

    Task WriteStateAsync(
        string grainType,
        GrainId grainId,
        IGrainState<TState> grainState);

    Task ClearStateAsync(
        string grainType,
        GrainId grainId,
        IGrainState<TState> grainState);
}
```

#### Step 2: Create IStorageAdapterFactory

```csharp
// Orleans.Core/Storage/IStorageAdapterFactory.cs
public interface IStorageAdapterFactory
{
    IStorageAdapter<TState> CreateAdapter<TState>(
        string name,
        IGrainStorageSerializer serializer);
}

// Example:
public class SqlStorageAdapterFactory : IStorageAdapterFactory
{
    private readonly IOptions<SqlStorageOptions> _options;

    public IStorageAdapter<TState> CreateAdapter<TState>(
        string name,
        IGrainStorageSerializer serializer)
    {
        var options = _options.Get(name);
        return new SqlStorageAdapter<TState>(options, serializer);
    }
}
```

#### Step 3: Adapt Existing IGrainStorage

```csharp
// Orleans.Core/Storage/GrainStorageAdapter.cs
internal sealed class GrainStorageAdapter<TState> : IStorageAdapter<TState>
{
    private readonly IGrainStorage _storage;
    private readonly IGrainStorageSerializer _serializer;

    public async Task<TState> ReadStateAsync(
        string grainType,
        GrainId grainId,
        IGrainState<TState> grainState)
    {
        await _storage.ReadStateAsync(grainType, grainId, grainState);
        return grainState.State;
    }

    public Task WriteStateAsync(
        string grainType,
        GrainId grainId,
        IGrainState<TState> grainState)
    {
        return _storage.WriteStateAsync(grainType, grainId, grainState);
    }

    public Task ClearStateAsync(
        string grainType,
        GrainId grainId,
        IGrainState<TState> grainState)
    {
        return _storage.ClearStateAsync(grainType, grainId, grainState);
    }
}
```

### Target Module Structure

- `Orleans.Core/Storage/IStorageAdapter.cs`
- `Orleans.Core/Storage/IStorageAdapterFactory.cs`
- `Orleans.Core/Storage/GrainStorageAdapter.cs` (adapts existing IGrainStorage)
- Provider implementations use factories and inject serializers via DI

## Transactions and Activation

### Problem Analysis

`TransactionAgent` is tightly coupled to activation lifecycle and messaging infrastructure, making the transaction protocol difficult to understand and test independently.

### Refactoring Strategy

#### Step 1: Extract TransactionCoordinatorService

```csharp
// Orleans.Transactions/Coordination/ITransactionCoordinatorService.cs
public interface ITransactionCoordinatorService
{
    Task<TransactionInfo> StartTransaction(TransactionStartOptions options);
    Task PrepareTransaction(TransactionId transactionId, IReadOnlyList<ITransactionParticipant> participants);
    Task CommitTransaction(TransactionId transactionId);
    Task AbortTransaction(TransactionId transactionId, TransactionAbortReason reason);
}

// Pure coordination logic, no messaging dependencies
internal sealed class TransactionCoordinatorService : ITransactionCoordinatorService
{
    // Pure orchestration of transaction protocol
}
```

#### Step 2: Introduce ITransactionalGrainContext

```csharp
// Orleans.Transactions.Abstractions/ITransactionalGrainContext.cs
public interface ITransactionalGrainContext
{
    TransactionInfo CurrentTransaction { get; }
    Task<T> ExecuteTransactional<T>(Func<Task<T>> operation);
    ITransactionalState<TState> GetTransactionalState<TState>(string stateName);
}
```

#### Step 3: Adapt TransactionAgent as Messaging Bridge

```csharp
// Orleans.Transactions/DistributedTM/TransactionAgent.cs (refactored)
internal sealed class TransactionAgent : SystemTarget, ITransactionAgent
{
    private readonly ITransactionCoordinatorService _coordinator;

    // TransactionAgent becomes a thin adapter between messaging and coordinator
    public Task<TransactionInfo> StartTransaction(
        bool readOnly,
        TimeSpan timeout)
    {
        return _coordinator.StartTransaction(new TransactionStartOptions
        {
            ReadOnly = readOnly,
            Timeout = timeout
        });
    }

    // Other methods delegate to coordinator
}
```

### Target Module Structure

- `Orleans.Transactions/Coordination/ITransactionCoordinatorService.cs`: Pure protocol logic.
- `Orleans.Transactions/Coordination/TransactionCoordinatorService.cs`: Implementation.
- `Orleans.Transactions.Abstractions/ITransactionalGrainContext.cs`: Grain-facing abstraction.
- `Orleans.Transactions/DistributedTM/TransactionAgent.cs`: Messaging adapter (reduced complexity).

## Cross-Cutting Observability

### Problem Analysis

Observability concerns (logging, metrics, tracing) are scattered throughout the codebase with inline logging statements and ad-hoc instrumentation, making it difficult to:
- Apply consistent observability patterns.
- Control logging verbosity centrally.
- Evolve telemetry schema without touching business logic.

### Refactoring Strategy

#### Step 1: Introduce ObservabilityContext

```csharp
// Orleans.Runtime.Observability/ObservabilityContext.cs
public sealed class ObservabilityContext
{
    public Activity Activity { get; }
    public ILogger Logger { get; }
    public Dictionary<string, object> Properties { get; }

    public void RecordEvent(string eventName, params (string Key, object Value)[] properties);
    public void RecordMetric(string metricName, long value, params (string Key, object Value)[] tags);
}
```

#### Step 2: Create InstrumentationService

```csharp
// Orleans.Runtime.Observability/InstrumentationService.cs
public interface IInstrumentationService
{
    ObservabilityContext CreateContext(string operationName);
    void RecordOperation(string operationName, TimeSpan duration, bool success);
}

internal sealed class InstrumentationService : IInstrumentationService
{
    private readonly ActivitySource _activitySource;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Meter _meter;

    public ObservabilityContext CreateContext(string operationName)
    {
        var activity = _activitySource.StartActivity(operationName);
        var logger = _loggerFactory.CreateLogger(operationName);

        return new ObservabilityContext
        {
            Activity = activity,
            Logger = logger,
            Properties = new Dictionary<string, object>()
        };
    }

    public void RecordOperation(
        string operationName,
        TimeSpan duration,
        bool success)
    {
        _meter.CreateHistogram<long>($"{operationName}.duration")
            .Record((long)duration.TotalMilliseconds, new KeyValuePair<string, object>("success", success));
    }
}
```

#### Step 3: Apply Middleware/Interceptors

```csharp
// Orleans.Runtime/Messaging/ObservabilityMiddleware.cs
internal sealed class ObservabilityMiddleware : IIncomingGrainCallFilter
{
    private readonly IInstrumentationService _instrumentation;

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        using var observabilityContext = _instrumentation.CreateContext(
            $"Grain.{context.InterfaceMethod.Name}");

        var sw = ValueStopwatch.StartNew();
        bool success = false;

        try
        {
            await context.Invoke();
            success = true;
        }
        finally
        {
            _instrumentation.RecordOperation(
                $"Grain.{context.InterfaceMethod.Name}",
                sw.Elapsed,
                success);
        }
    }
}
```

#### Step 4: Centralize OpenTelemetry Setup

```csharp
// Orleans.Runtime/Hosting/ObservabilityServiceCollectionExtensions.cs
public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddOrleansObservability(
        this IServiceCollection services,
        Action<ObservabilityOptions> configure = null)
    {
        services.AddOpenTelemetry()
            .WithTracing(builder => builder
                .AddSource("Orleans.Runtime")
                .AddSource("Orleans.Grains"))
            .WithMetrics(builder => builder
                .AddMeter("Orleans.Runtime")
                .AddMeter("Orleans.Grains"));

        services.AddSingleton<IInstrumentationService, InstrumentationService>();
        services.AddSingleton<ObservabilityMiddleware>();

        return services;
    }
}
```

### Target Module Structure

- `Orleans.Runtime.Observability/ObservabilityContext.cs`
- `Orleans.Runtime.Observability/IInstrumentationService.cs`
- `Orleans.Runtime.Observability/InstrumentationService.cs`
- `Orleans.Runtime/Messaging/ObservabilityMiddleware.cs`
- `Orleans.Runtime/Hosting/ObservabilityServiceCollectionExtensions.cs`

## Summary

These refactoring strategies provide concrete steps to decouple tightly coupled areas of Orleans-4316:

1. **Catalog**: Extract lifecycle, directory client, coordinator, and topology observer.
2. **MembershipTableManager**: Extract table accessor, gossip service, suspect/kill service, local lifecycle manager, and orchestrator.
3. **GrainDirectoryPartition**: Extract range lock manager, snapshot transfer service, and recovery service.
4. **Streaming**: Introduce provider factory and configuration service abstractions.
5. **Storage**: Introduce storage adapter and factory patterns with serializer injection.
6. **Transactions**: Extract coordinator service and introduce grain context abstraction.
7. **Observability**: Centralize telemetry via instrumentation service and middleware.

Each refactoring maintains backward compatibility while establishing cleaner boundaries for future evolution.
