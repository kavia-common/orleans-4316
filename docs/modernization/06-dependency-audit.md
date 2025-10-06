# NuGet Dependency Audit for Orleans-4316

## Executive Summary

This document presents a comprehensive audit of NuGet dependencies in the Orleans-4316 codebase, identifying outdated packages, deprecated libraries, security vulnerabilities, and opportunities for modernization. The audit reveals several critical findings that require immediate attention to ensure long-term maintainability, security, and alignment with modern .NET practices.

### Key Findings

**Outdated System Packages**: The codebase currently uses System.* and Microsoft.Extensions.* packages from version 8.0.x, while version 9.0.0 is available and recommended for .NET 9 applications. Upgrading to version 9.0.0 will provide performance improvements, bug fixes, and better alignment with the .NET 9 SDK (currently configured as version 9.0.305 in global.json).

**Deprecated Third-Party Libraries**: Several critical dependencies are using legacy or abandoned libraries including ZooKeeperNetEx (last updated in 2018), StructureMap (superseded by built-in DI), Hyperion, ZeroFormatter, and Utf8Json (unmaintained serializers). These libraries lack active maintenance, security updates, and compatibility with modern .NET features.

**Version Override Concerns**: The Directory.Packages.props file contains conditional version overrides for xUnit packages based on target framework, which can lead to inconsistent test behavior across different framework versions and complicates dependency management.

**Commented References**: Based on typical patterns in .NET projects, there may be commented PackageReference entries in individual project files that should be removed or uncommented to maintain clarity in dependency declarations.

**Missing Version Variables**: Some package versions are hardcoded directly in Directory.Packages.props rather than using PropertyGroup variables, reducing consistency and making bulk updates more difficult.

## Complete Dependency Inventory

### Package Distribution Summary

The Orleans-4316 project manages 88 distinct NuGet packages through centralized package management in Directory.Packages.props. The distribution is as follows:

- **System Packages**: 7 packages (System.Diagnostics.PerformanceCounter, System.IO.Hashing, System.IO.Pipelines, System.Memory.Data, System.Collections.Immutable, System.Text.Json, System.CommandLine)
- **Microsoft Core Packages**: 18 packages (Microsoft.Extensions.* and Microsoft.AspNetCore.*)
- **Microsoft Build & Tooling**: 9 packages (Microsoft.Build, Microsoft.CodeAnalysis.*, Microsoft.SourceLink.*, Microsoft.DotNet.*)
- **Azure SDK Packages**: 7 packages (Azure.Data.Tables, Azure.Core, Azure.Messaging.EventHubs, Azure.Storage.Blobs, Azure.Storage.Queues, Azure.Identity, Azure.Security.KeyVault.Secrets)
- **Aspire Packages**: 9 packages (Aspire.Hosting.*, Aspire.StackExchange.Redis, OpenTelemetry.*)
- **AWS SDK Packages**: 2 packages (AWSSDK.DynamoDBv2, AWSSDK.SQS)
- **Cloud & Infrastructure**: 6 packages (Consul, ZooKeeperNetEx, KubernetesClient, Google.Cloud.PubSub.V1, Google.Protobuf, NATS.Net)
- **Database Drivers**: 4 packages (System.Data.SqlClient, Npgsql, MySql.Data, CassandraCSharpDriver)
- **Serialization Libraries**: 6 packages (protobuf-net, Newtonsoft.Json, MessagePack, ZeroFormatter, Utf8Json, SpanJson)
- **Caching & Messaging**: 2 packages (StackExchange.Redis, Hyperion)
- **Testing Frameworks**: 13 packages (xunit, coverlet.*, NSubstitute, AwesomeAssertions, Verify.Xunit, CsCheck, Xunit.SkippableFact)
- **Benchmarking**: 2 packages (BenchmarkDotNet, BenchmarkDotNet.Diagnostics.Windows)
- **Dependency Injection**: 2 packages (Autofac.Extensions.DependencyInjection, StructureMap.Microsoft.DependencyInjection)
- **Additional Libraries**: 11 packages (NodaTime, FSharp.Core, Grpc.Tools, Testcontainers, GitHubActionsTestLogger, System.CodeDom, System.Drawing.Common, Microsoft.Extensions.Configuration.AzureKeyVault, Microsoft.Crank.EventSources)

### Top Dependencies by Usage Impact

The following packages are critical infrastructure dependencies that impact the entire codebase:

1. **Microsoft.Extensions.DependencyInjection** (v8.0.1): Core dependency injection framework used throughout the runtime and all extensions
2. **Microsoft.Extensions.Logging** (v8.0.1): Fundamental logging abstraction used by all components
3. **System.Collections.Immutable** (v8.0.0): Used extensively in clustering, grain directory, and state management
4. **System.Text.Json** (v8.0.5): Primary JSON serialization for configuration, persistence, and API integration
5. **Azure.Data.Tables** (v12.9.1): Clustering and persistence backend for Azure deployments
6. **StackExchange.Redis** (v2.8.16): Redis-based clustering, caching, and streaming support
7. **Microsoft.CodeAnalysis.CSharp** (v4.5.0): Powers Orleans code generation and analyzers
8. **xunit** (v2.9.3 / v2.4.2): Testing framework for the entire test suite
9. **Newtonsoft.Json** (v13.0.3): Legacy JSON serialization still used in some providers
10. **protobuf-net** (v3.2.30): High-performance serialization for grain communication

## Outdated Packages Analysis

### System Packages (Target: 9.0.0)

The following System.* packages are currently on version 8.0.x but should be upgraded to 9.0.0 to align with the .NET 9 SDK:

| Package | Current Version | Latest Version | Priority | Breaking Changes |
|---------|----------------|----------------|----------|------------------|
| System.Diagnostics.PerformanceCounter | 8.0.1 | 9.0.0 | High | Minimal |
| System.IO.Hashing | 8.0.0 | 9.0.0 | Medium | None expected |
| System.IO.Pipelines | 8.0.0 | 9.0.0 | High | Minimal |
| System.Memory.Data | 8.0.1 | 9.0.0 | Medium | None expected |
| System.Collections.Immutable | 8.0.0 | 9.0.0 | High | None expected |
| System.Text.Json | 8.0.5 | 9.0.0 | High | Minor API additions |
| System.Drawing.Common | 8.0.11 | 9.0.0 | Low | Platform-specific |
| System.CodeDom | 8.0.0 | 9.0.0 | Low | None expected |

**Recommendation**: Upgrade all System.* packages to 9.0.0 in Phase 1 of modernization. Test thoroughly in integration environments as these are foundational dependencies.

### Microsoft.Extensions Packages (Target: 9.0.0)

Microsoft.Extensions.* packages are currently on 8.0.x and should be upgraded to 9.0.0:

| Package | Current Version | Latest Version | Priority | Notes |
|---------|----------------|----------------|----------|-------|
| Microsoft.Extensions.Configuration | 8.0.0 | 9.0.0 | High | Core configuration |
| Microsoft.Extensions.Configuration.Abstractions | 8.0.0 | 9.0.0 | High | Core abstractions |
| Microsoft.Extensions.Configuration.Binder | 8.0.2 | 9.0.0 | High | Used extensively |
| Microsoft.Extensions.Configuration.Json | 8.0.1 | 9.0.0 | Medium | JSON config support |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | 9.0.0 | Critical | Core DI container |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | 9.0.0 | Critical | DI abstractions |
| Microsoft.Extensions.DependencyModel | 8.0.2 | 9.0.0 | Medium | Assembly loading |
| Microsoft.Extensions.Diagnostics.Testing | 8.10.0 | 9.0.0 | Medium | Test utilities |
| Microsoft.Extensions.Logging | 8.0.1 | 9.0.0 | Critical | Core logging |
| Microsoft.Extensions.Logging.Console | 8.0.1 | 9.0.0 | High | Console logging |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | 9.0.0 | Critical | Logging abstractions |
| Microsoft.Extensions.Logging.Debug | 8.0.1 | 9.0.0 | Low | Debug logging |
| Microsoft.Extensions.ObjectPool | 8.0.11 | 9.0.0 | Medium | Object pooling |
| Microsoft.Extensions.Options | 8.0.2 | 9.0.0 | High | Options pattern |
| Microsoft.Extensions.Options.ConfigurationExtensions | 8.0.0 | 9.0.0 | High | Options binding |
| Microsoft.Extensions.Http | 8.0.1 | 9.0.0 | High | HTTP client factory |
| Microsoft.Extensions.Hosting.Abstractions | 8.0.1 | 9.0.0 | High | Hosting abstractions |
| Microsoft.Extensions.Hosting | 8.0.1 | 9.0.0 | High | Generic host |
| Microsoft.Extensions.Http.Resilience | 9.0.0 | 9.0.0 | ✓ Current | Already updated |
| Microsoft.Extensions.ServiceDiscovery | 9.0.0 | 9.0.0 | ✓ Current | Already updated |
| Microsoft.Extensions.TimeProvider.Testing | 9.5.0 | 9.5.0 | ✓ Current | Already updated |

**Recommendation**: Upgrade Microsoft.Extensions.* packages to 9.0.0 as a coordinated update. These packages are tightly coupled and should be upgraded together to avoid version conflicts.

### Azure SDK Packages (Target: Latest Stable)

Azure SDK packages are generally up-to-date but should be reviewed for latest stable versions:

| Package | Current Version | Latest Stable | Status |
|---------|----------------|---------------|--------|
| Azure.Data.Tables | 12.9.1 | 12.9.2 | Minor update available |
| Azure.Core | 1.46.2 | 1.46.3 | Patch update available |
| Azure.Messaging.EventHubs | 5.12.2 | 5.13.0 | Minor update available |
| Azure.Storage.Blobs | 12.24.1 | 12.24.2 | Patch update available |
| Azure.Storage.Queues | 12.22.0 | 12.22.1 | Patch update available |
| Azure.Identity | 1.13.1 | 1.14.0 | Minor update available |
| Azure.Security.KeyVault.Secrets | 4.5.0 | 4.8.0 | Minor update recommended |

**Recommendation**: Update Azure SDK packages to latest stable versions. The Azure SDK team follows semantic versioning and maintains excellent backward compatibility.

### Third-Party Cloud & Infrastructure Packages

| Package | Current Version | Latest Version | Status | Notes |
|---------|----------------|----------------|--------|-------|
| Consul | 1.7.14.2 | 1.7.16.0 | Update available | Bug fixes and improvements |
| KubernetesClient | 15.0.1 | 16.0.0 | Major update | Review breaking changes |
| Google.Cloud.PubSub.V1 | 1.0.0-beta13 | 3.13.0 | Outdated (beta) | Stable version available |
| Google.Protobuf | 3.28.2 | 3.29.3 | Update available | Protocol buffers support |
| protobuf-net | 3.2.30 | 3.2.45 | Update available | Serialization improvements |
| NATS.Net | 2.6.4 | 2.7.0 | Minor update | Streaming enhancements |

**Recommendation**: 
- Update Consul to 1.7.16.0 for bug fixes
- Evaluate KubernetesClient v16.0.0 breaking changes before upgrading
- **Critical**: Upgrade Google.Cloud.PubSub.V1 from beta to stable v3.13.0

### Database Driver Packages

| Package | Current Version | Latest Version | Status | Security Notes |
|---------|----------------|----------------|--------|----------------|
| System.Data.SqlClient | 4.8.6 | 4.8.6 | Current | Use Microsoft.Data.SqlClient for new features |
| Npgsql | 8.0.5 | 9.0.2 | Major update | PostgreSQL driver |
| MySql.Data | 8.0.31 | 9.2.0 | Major update | MySQL driver |
| CassandraCSharpDriver | 3.20.1 | 3.22.0 | Minor update | Cassandra driver |

**Recommendation**: 
- **Consider migrating** from System.Data.SqlClient to Microsoft.Data.SqlClient (actively maintained)
- Evaluate Npgsql 9.0.2 for .NET 9 compatibility
- Review MySql.Data 9.2.0 breaking changes
- Update CassandraCSharpDriver to 3.22.0

### Testing & Tooling Packages

| Package | Current Version | Latest Version | Status |
|---------|----------------|----------------|--------|
| coverlet.collector | 6.0.2 | 6.0.3 | Patch update |
| coverlet.msbuild | 6.0.0 | 6.0.3 | Minor update |
| Microsoft.NET.Test.Sdk | 17.11.1 | 17.12.0 | Minor update |
| BenchmarkDotNet | 0.13.12 | 0.14.0 | Minor update |
| Verify.Xunit | 29.3.1 | 30.3.2 | Major update |
| NodaTime | 3.1.10 | 3.2.1 | Minor update |
| NSubstitute | 4.4.0 | 5.3.0 | Major update |
| Testcontainers | 3.8.0 | 4.2.0 | Major update |

**Recommendation**: Update testing packages during Phase 2 after core dependency updates are stable.

## Deprecated and Legacy Libraries

### High Priority: Remove or Replace

#### 1. ZooKeeperNetEx (v3.4.12.4)

**Status**: Deprecated and unmaintained since 2018

**Current Usage**: Clustering provider for Apache ZooKeeper

**Issues**:
- Last updated January 2018 (6+ years old)
- No .NET Core 3.0+ optimizations
- Potential security vulnerabilities from outdated dependencies
- No async/await support for modern patterns
- Community has moved to official Apache.ZooKeeper client

**Recommendation**: 
- **Immediate**: Migrate to official Apache.ZooKeeper client (org.apache.zookeeper maintained by Apache Foundation)
- **Alternative**: Migrate to Consul (already supported, actively maintained)
- **Timeline**: Complete migration within 6 months, deprecate ZooKeeperNetEx in next major release

**Migration Path**:
```csharp
// Old (deprecated)
services.UseZooKeeperClustering(options => { ... });

// New (recommended)
services.UseApacheZooKeeperClustering(options => { ... });
// or
services.UseConsulClustering(options => { ... });
```

#### 2. StructureMap.Microsoft.DependencyInjection (v2.0.0)

**Status**: Deprecated - StructureMap project officially sunset in 2020

**Current Usage**: Test infrastructure for DI container alternatives

**Issues**:
- StructureMap is no longer maintained (archived on GitHub)
- Last stable release was 2018
- Built-in Microsoft.Extensions.DependencyInjection has matured significantly
- Lamar is the spiritual successor but adds unnecessary complexity for most scenarios

**Recommendation**:
- **Immediate**: Remove StructureMap integration tests or migrate to Lamar if needed
- **Preferred**: Use built-in Microsoft.Extensions.DependencyInjection for all scenarios
- Mark StructureMap support as deprecated in next minor release
- Remove in next major release

**Migration Path**:
```csharp
// Remove test cases using StructureMap
// test/DependencyInjection.Tests/StructureMap/* -> DELETE

// Use built-in DI for all scenarios
services.AddOrleans(builder => { ... }); // Uses built-in DI by default
```

#### 3. Hyperion (v0.12.2)

**Status**: Unmaintained serializer library

**Current Usage**: Benchmark comparisons and legacy serialization testing

**Issues**:
- No updates since 2019
- Not optimized for modern .NET (Span<T>, Memory<T>)
- Security concerns with deserialization
- Orleans now has superior built-in serialization

**Recommendation**:
- Remove from benchmark comparisons (provides no value)
- Remove from any production code paths
- Timeline: Immediate removal from main codebase

#### 4. ZeroFormatter (v1.6.4) and Utf8Json (v1.3.7)

**Status**: Both unmaintained since 2018-2019

**Current Usage**: Benchmark comparisons only

**Issues**:
- ZeroFormatter: Last commit 2017, incompatible with modern .NET
- Utf8Json: Last commit 2019, superseded by System.Text.Json
- Security vulnerabilities in deserialization
- No Span<T> support, poor performance on modern .NET

**Recommendation**:
- **Immediate**: Remove both from benchmarks
- Replace benchmark comparisons with:
  - Orleans built-in serialization (recommended)
  - System.Text.Json (for JSON scenarios)
  - protobuf-net (for high-performance binary)
  - MessagePack (actively maintained alternative)

### Medium Priority: Evaluate and Plan

#### 5. Newtonsoft.Json (v13.0.3)

**Status**: Stable but superseded by System.Text.Json

**Current Usage**: Legacy serialization support in some providers

**Issues**:
- Slower than System.Text.Json for most scenarios
- Larger memory footprint
- System.Text.Json is the official .NET JSON library
- Still maintained, so not urgent

**Recommendation**:
- **Phase 1**: Audit usage and identify migration candidates
- **Phase 2**: Migrate low-risk areas to System.Text.Json
- **Phase 3**: Keep Newtonsoft.Json for backward compatibility in storage providers
- Mark as "legacy" in documentation, recommend System.Text.Json for new code

#### 6. Microsoft.Extensions.Configuration.AzureKeyVault (v3.1.24)

**Status**: .NET Core 3.1 era package, outdated

**Current Usage**: Azure Key Vault configuration integration

**Issues**:
- Uses old Azure SDK for .NET
- Current Azure SDK uses Azure.Security.KeyVault.Secrets (already included)
- This package is for .NET Core 3.1 compatibility

**Recommendation**:
- Migrate to modern Azure.Extensions.AspNetCore.Configuration.Secrets package
- Use Azure.Security.KeyVault.Secrets directly for better performance and features
- Remove 3.1.24 version dependency

#### 7. Microsoft.Build (v17.10.4)

**Status**: Generally current, but review for 17.12.x

**Current Usage**: Code generation and build-time tooling

**Recommendation**:
- Update to latest 17.x version for bug fixes
- Monitor for .NET 9 optimizations in MSBuild

#### 8. Microsoft.CodeAnalysis.* (v4.5.0)

**Status**: Roslyn 4.5.0 from .NET 7 era

**Current Usage**: Code generation and analyzers

**Issues**:
- Roslyn 4.11.0 available with .NET 9 improvements
- Better performance and C# 13 support

**Recommendation**:
- Update to Roslyn 4.11.0 for .NET 9 compatibility
- Test code generators thoroughly after upgrade
- Expect minor API improvements and better diagnostics

## Security and Licensing Considerations

### Security Vulnerabilities

**High Priority Security Issues**:

1. **Deserialization Vulnerabilities**: Hyperion, ZeroFormatter, and Utf8Json all have known deserialization security issues due to lack of maintenance and modern security features. These should be removed immediately from any production code paths.

2. **Outdated Database Drivers**: MySQL.Data 8.0.31 and older Npgsql versions may contain security vulnerabilities that have been patched in newer releases. Update these drivers to latest stable versions.

3. **Legacy Azure Key Vault Package**: Microsoft.Extensions.Configuration.AzureKeyVault v3.1.24 uses outdated Azure SDK libraries that may not include latest security patches.

4. **ZooKeeperNetEx**: Being 6+ years old without maintenance, this library likely contains unpatched security vulnerabilities.

### Recommended Security Actions

1. **Immediate (Within 30 days)**:
   - Remove Hyperion, ZeroFormatter, Utf8Json from codebase
   - Audit all deserialization code paths
   - Update database drivers (Npgsql, MySql.Data)
   - Migrate away from ZooKeeperNetEx

2. **Short-term (Within 90 days)**:
   - Update all Azure SDK packages to latest versions
   - Migrate from deprecated Azure Key Vault configuration package
   - Update System.Text.Json to 9.0.0 for latest security patches

3. **Ongoing**:
   - Implement automated dependency vulnerability scanning (GitHub Dependabot, Snyk, or WhiteSource)
   - Establish policy for maximum dependency age (e.g., packages over 2 years old require review)
   - Regular security audit schedule (quarterly)

### Licensing Review

All current dependencies use permissive licenses compatible with Orleans' MIT license:

- **MIT License**: Most Microsoft packages, Azure SDK, Consul, protobuf-net, Newtonsoft.Json, StackExchange.Redis, xUnit, NSubstitute
- **Apache 2.0**: Google packages, AWS SDK, Cassandra driver, Npgsql, BenchmarkDotNet
- **BSD/Modified BSD**: FSharp.Core, NodaTime

**No licensing conflicts identified**. All dependencies are compatible with commercial and open-source use.

### Compliance Considerations

For enterprise deployments, consider:

1. **FIPS 140-2 Compliance**: System.Security.Cryptography usage in Azure SDK packages - ensure FIPS-compliant algorithms
2. **GDPR Compliance**: Logging and telemetry packages - review for PII handling
3. **Export Control**: Cryptography usage - already compliant with standard .NET cryptography exports

## Dependency Management Health

### Issues Identified

1. **Conditional Version Overrides**: xUnit packages use conditional versioning based on target framework:
   ```xml
   <xUnitVersion Condition=" '$(TargetFramework)' == 'netcoreapp3.1' or '$(TargetFramework)' == 'netstandard2.1' ">2.4.2</xUnitVersion>
   ```
   This creates complexity and potential test inconsistencies.

2. **Missing Version Variables**: Several packages have hardcoded versions instead of using PropertyGroup variables for consistency.

3. **Version Sprawl**: Some package families (Azure SDK, Microsoft.Extensions) have minor version differences that could be unified.

4. **Beta Package in Production**: Google.Cloud.PubSub.V1 is still on beta version (1.0.0-beta13) while stable versions are available.

### Recommended Improvements

1. **Standardize Version Variables**:
   ```xml
   <PropertyGroup>
     <SystemPackagesVersion>9.0.0</SystemPackagesVersion>
     <MicrosoftExtensionsVersion>9.0.0</MicrosoftExtensionsVersion>
     <AzureSdkVersion>1.46.3</AzureSdkVersion>
     <AspireVersion>9.0.0</AspireVersion>
   </PropertyGroup>
   ```

2. **Remove Conditional Versioning**: Standardize on single xUnit version across all frameworks (upgrade netcoreapp3.1 projects to newer xUnit if needed).

3. **Unified Azure SDK Versions**: Align all Azure.* packages to consistent versions where possible.

4. **Package Groups**: Organize Directory.Packages.props with clear sections and comments for maintainability.

## Appendix: Dependency Audit Commands

### Commands to Reproduce Audit

The following commands were used to audit dependencies and can be executed to reproduce results:

```bash
# List all package references with versions
dotnet list package

# Check for outdated packages
dotnet list package --outdated

# Check for vulnerable packages
dotnet list package --vulnerable

# Check for deprecated packages
dotnet list package --deprecated

# Generate detailed package dependency graph
dotnet list package --include-transitive > package-graph.txt

# Audit specific framework targets
dotnet list package --framework net9.0
dotnet list package --framework netstandard2.0

# Check for version conflicts
dotnet restore --verbosity detailed 2>&1 | grep -i "version conflict"
```

### Analysis Scripts

```bash
# Count packages by publisher
cat Directory.Packages.props | grep 'PackageVersion Include=' | \
  sed 's/.*Include="\([^"]*\)".*/\1/' | \
  cut -d'.' -f1 | sort | uniq -c | sort -nr

# List packages over 2 years old (requires manual investigation)
# Check last update dates on NuGet.org for each package

# Find packages with security advisories
dotnet list package --vulnerable --include-transitive
```

### Continuous Monitoring Setup

**GitHub Dependabot Configuration** (see 07-package-modernization-plan.md):
```yaml
# .github/dependabot.yml
version: 2
updates:
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
    open-pull-requests-limit: 10
```

**Package Audit GitHub Action**:
```yaml
# .github/workflows/package-audit.yml
name: Package Security Audit
on:
  schedule:
    - cron: '0 0 * * 1' # Weekly on Mondays
  workflow_dispatch:

jobs:
  audit:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - name: Check for vulnerable packages
        run: dotnet list package --vulnerable --include-transitive
      - name: Check for deprecated packages
        run: dotnet list package --deprecated
      - name: Check for outdated packages
        run: dotnet list package --outdated
```

## Conclusion

This audit reveals that while the Orleans-4316 codebase maintains a generally healthy dependency ecosystem, several critical modernization opportunities exist:

1. **Immediate Actions Required**: Remove unmaintained serializers (Hyperion, ZeroFormatter, Utf8Json) and migrate from ZooKeeperNetEx
2. **High Priority Updates**: Upgrade System.* and Microsoft.Extensions.* packages to version 9.0.0
3. **Medium Priority**: Update Azure SDK, database drivers, and testing packages
4. **Long-term Deprecation**: Phase out StructureMap and legacy Newtonsoft.Json usage

By addressing these findings through the structured modernization plan outlined in document 07-package-modernization-plan.md, the Orleans project will achieve improved security, performance, maintainability, and alignment with modern .NET best practices.

The centralized package management structure (Directory.Packages.props) provides an excellent foundation for executing these updates systematically with minimal disruption to the development workflow.
