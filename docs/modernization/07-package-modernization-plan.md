# Dependency Modernization Plan for Orleans-4316

## Overview

This document outlines a comprehensive, actionable plan for modernizing the NuGet dependencies in the Orleans-4316 codebase. Building upon the findings from the Dependency Audit (06-dependency-audit.md), this plan provides specific steps, timelines, code examples, and automation strategies to achieve a modern, secure, and maintainable dependency ecosystem.

The modernization effort is structured around three core goals: upgrading to .NET 9 package versions, removing deprecated and unmaintained libraries, and establishing robust automation for ongoing dependency management. This plan balances the need for rapid security improvements with careful risk management to maintain production stability.

## Goals and Principles

### Primary Goals

1. **Security First**: Eliminate all known security vulnerabilities by removing unmaintained packages and upgrading to latest stable versions with security patches.

2. **Modern .NET 9 Alignment**: Upgrade all Microsoft packages to version 9.0.0 to leverage performance improvements, new features, and ensure compatibility with the .NET 9 SDK (currently configured as 9.0.305).

3. **Simplified Dependency Management**: Standardize version variables, remove conditional overrides, and establish clear patterns for future dependency additions.

4. **Reproducible Builds**: Ensure deterministic build outcomes through proper version pinning and transitive dependency management.

5. **Automated Maintenance**: Implement CI/CD pipelines and Dependabot configuration to continuously monitor and update dependencies with minimal manual intervention.

### Guiding Principles

**Backward Compatibility**: Maintain API compatibility during dependency updates. Where breaking changes are unavoidable, provide clear migration paths and deprecation warnings spanning at least two minor releases.

**Incremental Migration**: Adopt a phased approach with clear success criteria for each phase. Enable rolling back individual changes without affecting the entire modernization effort.

**Test-Driven Updates**: Every dependency update must pass the full test suite. Performance benchmarks must show no regressions exceeding 5%. Integration tests must validate provider functionality.

**Documentation First**: Document all breaking changes, migration steps, and deprecated features before releasing updates. Provide sample code and migration scripts where applicable.

**Community Communication**: Announce deprecation plans and breaking changes at least 6 months in advance. Gather feedback from major Orleans users before finalizing decisions.

## Proposed Directory.Packages.props Updates

### Target State Overview

The updated Directory.Packages.props will feature:

- **Unified version variables** for package families (System.*, Microsoft.Extensions.*, Azure.*, etc.)
- **Version 9.0.0** for all Microsoft.Extensions and System packages
- **Removal of conditional versioning** for xUnit and other test packages
- **Clear deprecation markers** for packages scheduled for removal
- **Organized sections** with comments for maintainability

### Proposed Directory.Packages.props Structure

Below is the proposed updated structure with version variables and organized sections:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
    
    <!-- Version Variables for Consistency -->
    <SystemPackagesVersion>9.0.0</SystemPackagesVersion>
    <MicrosoftExtensionsVersion>9.0.0</MicrosoftExtensionsVersion>
    <MicrosoftAspNetCoreVersion>9.0.0</MicrosoftAspNetCoreVersion>
    <AspireVersion>9.0.0</AspireVersion>
    <AzureCoreVersion>1.46.3</AzureCoreVersion>
    <AzureStorageVersion>12.24.2</AzureStorageVersion>
    <AzureDataTablesVersion>12.9.2</AzureDataTablesVersion>
    <AzureMessagingVersion>5.13.0</AzureMessagingVersion>
    <AzureIdentityVersion>1.14.0</AzureIdentityVersion>
    <OpenTelemetryVersion>1.9.0</OpenTelemetryVersion>
    <xUnitVersion>2.9.3</xUnitVersion>
    <xUnitRunnerVersion>2.8.2</xUnitRunnerVersion>
    <CodeAnalysisVersion>4.11.0</CodeAnalysisVersion>
    <BenchmarkDotNetVersion>0.14.0</BenchmarkDotNetVersion>
  </PropertyGroup>

  <ItemGroup Label="System Packages">
    <PackageVersion Include="System.Diagnostics.PerformanceCounter" Version="$(SystemPackagesVersion)" />
    <PackageVersion Include="System.IO.Hashing" Version="$(SystemPackagesVersion)" NoWarn="NU5104" />
    <PackageVersion Include="System.IO.Pipelines" Version="$(SystemPackagesVersion)" />
    <PackageVersion Include="System.Memory.Data" Version="$(SystemPackagesVersion)" />
    <PackageVersion Include="System.Collections.Immutable" Version="$(SystemPackagesVersion)" />
    <PackageVersion Include="System.Text.Json" Version="$(SystemPackagesVersion)" />
    <PackageVersion Include="System.CodeDom" Version="$(SystemPackagesVersion)" />
    <PackageVersion Include="System.Drawing.Common" Version="$(SystemPackagesVersion)" />
    <PackageVersion Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
  </ItemGroup>

  <ItemGroup Label="Microsoft.Extensions Packages">
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Abstractions" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Binder" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Json" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.DependencyModel" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Diagnostics.Testing" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Console" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Debug" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.ObjectPool" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Options" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.TimeProvider.Testing" Version="9.5.0" />
    <PackageVersion Include="Microsoft.Extensions.Http" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Hosting.Abstractions" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="$(MicrosoftExtensionsVersion)" />
    <PackageVersion Include="Microsoft.Extensions.ServiceDiscovery" Version="$(MicrosoftExtensionsVersion)" />
  </ItemGroup>

  <ItemGroup Label="Microsoft ASP.NET Core Packages">
    <PackageVersion Include="Microsoft.AspNetCore.Connections.Abstractions" Version="$(MicrosoftAspNetCoreVersion)" />
  </ItemGroup>

  <ItemGroup Label="Microsoft Build and Analysis Packages">
    <PackageVersion Include="Microsoft.Build" Version="17.12.7" />
    <PackageVersion Include="Microsoft.CSharp" Version="4.7.0" />
    <PackageVersion Include="Microsoft.CodeAnalysis" Version="$(CodeAnalysisVersion)" />
    <PackageVersion Include="Microsoft.CodeAnalysis.Common" Version="$(CodeAnalysisVersion)" />
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="$(CodeAnalysisVersion)" />
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="$(CodeAnalysisVersion)" />
    <PackageVersion Include="Microsoft.CodeAnalysis.Workspaces.Common" Version="$(CodeAnalysisVersion)" />
    <PackageVersion Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" />
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing" Version="1.1.2" />
    <PackageVersion Include="Microsoft.DotNet.PlatformAbstractions" Version="3.1.6" />
    <PackageVersion Include="Microsoft.DotNet.GenAPI.Task" Version="9.0.103-servicing.25065.25" />
  </ItemGroup>

  <ItemGroup Label="Azure SDK Packages">
    <PackageVersion Include="Azure.Core" Version="$(AzureCoreVersion)" />
    <PackageVersion Include="Azure.Data.Tables" Version="$(AzureDataTablesVersion)" />
    <PackageVersion Include="Azure.Messaging.EventHubs" Version="$(AzureMessagingVersion)" />
    <PackageVersion Include="Azure.Storage.Blobs" Version="$(AzureStorageVersion)" />
    <PackageVersion Include="Azure.Storage.Queues" Version="12.22.1" />
    <PackageVersion Include="Azure.Identity" Version="$(AzureIdentityVersion)" />
    <PackageVersion Include="Azure.Security.KeyVault.Secrets" Version="4.8.0" />
  </ItemGroup>

  <ItemGroup Label="Aspire and OpenTelemetry Packages">
    <PackageVersion Include="Aspire.Azure.Storage.Queues" Version="$(AspireVersion)" />
    <PackageVersion Include="Aspire.Hosting.AppHost" Version="$(AspireVersion)" />
    <PackageVersion Include="Aspire.Hosting.Orleans" Version="$(AspireVersion)" />
    <PackageVersion Include="Aspire.Hosting.Redis" Version="$(AspireVersion)" />
    <PackageVersion Include="Aspire.StackExchange.Redis" Version="$(AspireVersion)" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="$(OpenTelemetryVersion)" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="$(OpenTelemetryVersion)" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="$(OpenTelemetryVersion)" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="$(OpenTelemetryVersion)" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="$(OpenTelemetryVersion)" />
  </ItemGroup>

  <ItemGroup Label="AWS SDK Packages">
    <PackageVersion Include="AWSSDK.DynamoDBv2" Version="3.7.400.7" />
    <PackageVersion Include="AWSSDK.SQS" Version="3.7.400.7" />
  </ItemGroup>

  <ItemGroup Label="Cloud Infrastructure Packages">
    <PackageVersion Include="Consul" Version="1.7.16.0" />
    <!-- DEPRECATED: ZooKeeperNetEx - Migrate to Apache.ZooKeeper or Consul by v9.2.0 -->
    <PackageVersion Include="ZooKeeperNetEx" Version="3.4.12.4" Condition="'$(UseDeprecatedZooKeeper)' == 'true'" />
    <PackageVersion Include="KubernetesClient" Version="15.0.1" />
    <PackageVersion Include="Google.Cloud.PubSub.V1" Version="3.13.0" />
    <PackageVersion Include="Google.Protobuf" Version="3.29.3" />
    <PackageVersion Include="NATS.Net" Version="2.7.0" />
  </ItemGroup>

  <ItemGroup Label="Database Drivers">
    <PackageVersion Include="System.Data.SqlClient" Version="4.8.6" />
    <PackageVersion Include="Npgsql" Version="9.0.2" />
    <PackageVersion Include="MySql.Data" Version="9.2.0" />
    <PackageVersion Include="CassandraCSharpDriver" Version="3.22.0" />
  </ItemGroup>

  <ItemGroup Label="Serialization Libraries">
    <PackageVersion Include="protobuf-net" Version="3.2.45" />
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageVersion Include="MessagePack" Version="2.5.187" />
    <PackageVersion Include="SpanJson" Version="4.0.1" />
    <!-- DEPRECATED: Remove in next major version - Use Orleans built-in serialization -->
    <!-- <PackageVersion Include="Hyperion" Version="0.12.2" /> REMOVED -->
    <!-- <PackageVersion Include="ZeroFormatter" Version="1.6.4" /> REMOVED -->
    <!-- <PackageVersion Include="Utf8Json" Version="1.3.7" /> REMOVED -->
  </ItemGroup>

  <ItemGroup Label="Caching and Messaging">
    <PackageVersion Include="StackExchange.Redis" Version="2.8.16" />
  </ItemGroup>

  <ItemGroup Label="Testing Frameworks">
    <PackageVersion Include="xunit" Version="$(xUnitVersion)" />
    <PackageVersion Include="xunit.assert" Version="$(xUnitVersion)" />
    <PackageVersion Include="xunit.extensibility.core" Version="$(xUnitVersion)" />
    <PackageVersion Include="xunit.extensibility.execution" Version="$(xUnitVersion)" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="$(xUnitRunnerVersion)" />
    <PackageVersion Include="Xunit.SkippableFact" Version="1.4.13" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="coverlet.collector" Version="6.0.3" />
    <PackageVersion Include="coverlet.msbuild" Version="6.0.3" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
    <PackageVersion Include="NSubstitute.Analyzers.CSharp" Version="1.0.17" />
    <PackageVersion Include="AwesomeAssertions" Version="8.0.2" />
    <PackageVersion Include="Verify.Xunit" Version="30.3.2" />
    <PackageVersion Include="CsCheck" Version="2.14.1" />
    <PackageVersion Include="Testcontainers" Version="4.2.0" />
    <PackageVersion Include="GitHubActionsTestLogger" Version="2.4.1" />
  </ItemGroup>

  <ItemGroup Label="Benchmarking">
    <PackageVersion Include="BenchmarkDotNet" Version="$(BenchmarkDotNetVersion)" />
    <PackageVersion Include="BenchmarkDotNet.Diagnostics.Windows" Version="$(BenchmarkDotNetVersion)" />
  </ItemGroup>

  <ItemGroup Label="Dependency Injection Alternatives">
    <PackageVersion Include="Autofac.Extensions.DependencyInjection" Version="10.0.0" />
    <!-- DEPRECATED: StructureMap - Remove in v10.0.0 -->
    <PackageVersion Include="StructureMap.Microsoft.DependencyInjection" Version="2.0.0" Condition="'$(UseDeprecatedStructureMap)' == 'true'" />
  </ItemGroup>

  <ItemGroup Label="Additional Libraries">
    <PackageVersion Include="NodaTime" Version="3.2.1" />
    <PackageVersion Include="FSharp.Core" Version="9.0.100" />
    <PackageVersion Include="Grpc.Tools" Version="2.67.0" />
    <PackageVersion Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" />
    <PackageVersion Include="Microsoft.Crank.EventSources" Version="0.2.0-alpha.23422.5" />
  </ItemGroup>

  <ItemGroup Label="Tooling and Build">
    <PackageVersion Include="Microsoft.SourceLink.AzureRepos.Git" Version="8.0.0" />
    <PackageVersion Include="Microsoft.SourceLink.GitHub" Version="8.0.0" />
  </ItemGroup>

  <!-- REMOVED PACKAGES - DO NOT RE-ADD WITHOUT SECURITY REVIEW -->
  <!-- Hyperion, ZeroFormatter, Utf8Json - Unmaintained serializers with security issues -->
  <!-- Microsoft.Extensions.Configuration.AzureKeyVault v3.1.24 - Use Azure.Security.KeyVault.Secrets -->
</Project>
```

### Key Changes Explained

1. **Version Variables**: All package families now use centralized version variables (e.g., `$(SystemPackagesVersion)`) making bulk updates trivial.

2. **Upgraded to 9.0.0**: System.*, Microsoft.Extensions.*, and ASP.NET Core packages upgraded to 9.0.0 for .NET 9 alignment.

3. **Removed Conditional Versioning**: xUnit no longer has framework-specific versions. All projects use xUnit 2.9.3.

4. **Updated Third-Party Packages**: 
   - Google.Cloud.PubSub.V1: 1.0.0-beta13 → 3.13.0 (stable)
   - Consul: 1.7.14.2 → 1.7.16.0
   - Npgsql: 8.0.5 → 9.0.2
   - MySql.Data: 8.0.31 → 9.2.0
   - Azure packages to latest stable versions

5. **Removed Deprecated Packages**: Hyperion, ZeroFormatter, Utf8Json completely removed.

6. **Conditional Deprecation**: ZooKeeperNetEx and StructureMap only included if opt-in property is set, with deprecation warnings.

7. **Clear Organization**: Labeled ItemGroups for easy navigation and maintenance.

8. **Documentation Comments**: Inline comments explain deprecations and removal timelines.

## Cleanup Tasks

### Task 1: Remove Conditional Version Overrides

**Current Issue**:
```xml
<xUnitVersion>2.9.3</xUnitVersion>
<xUnitVersion Condition=" '$(TargetFramework)' == 'netcoreapp3.1' or '$(TargetFramework)' == 'netstandard2.1' ">2.4.2</xUnitVersion>
```

**Action**: Remove conditional override and upgrade all projects to xUnit 2.9.3.

**Script**:
```bash
# Find all projects still targeting netcoreapp3.1 or netstandard2.1
find . -name "*.csproj" -exec grep -l "netcoreapp3.1\|netstandard2.1" {} \;

# For each project, verify xUnit compatibility or update target framework
# xUnit 2.9.3 supports netcoreapp3.1+, so no breaking changes expected
```

**Validation**:
```bash
# Run all tests to ensure xUnit 2.9.3 works across all frameworks
dotnet test --configuration Release --no-build
```

### Task 2: Define Missing Version Variables

**Current Issue**: Some packages have hardcoded versions instead of using variables.

**Action**: Convert all hardcoded versions to variables in PropertyGroup.

**Example**:
```xml
<!-- Before -->
<PackageVersion Include="Microsoft.Azure.Cosmos" Version="3.45.2" />

<!-- After -->
<PropertyGroup>
  <AzureCosmosVersion>3.45.2</AzureCosmosVersion>
</PropertyGroup>
<PackageVersion Include="Microsoft.Azure.Cosmos" Version="$(AzureCosmosVersion)" />
```

**Automated Script**:
```powershell
# PowerShell script to identify hardcoded versions
$packagesFile = "Directory.Packages.props"
[xml]$xml = Get-Content $packagesFile

$xml.Project.ItemGroup.PackageVersion | Where-Object { $_.Version -notmatch '^\$\(' } | 
    Select-Object Include, Version | 
    Format-Table -AutoSize

# Output candidates for variable extraction
```

### Task 3: Remove Commented PackageReference Entries

**Current Issue**: Project files may contain commented `<PackageReference>` entries that should be cleaned up.

**Action**: Scan all .csproj files and remove or uncomment references.

**Script**:
```bash
# Find commented PackageReference entries
find . -name "*.csproj" -exec grep -Hn "<!--.*PackageReference.*-->" {} \;

# Review and remove manually, or use sed (with caution)
# find . -name "*.csproj" -exec sed -i '/<!--.*PackageReference.*-->/d' {} \;
```

**Manual Review Required**: Some comments may be intentional (e.g., "DO NOT ADD THIS PACKAGE"). Review before deletion.

### Task 4: Audit Transitive Dependencies

**Current Issue**: Transitive dependencies may introduce outdated or vulnerable packages.

**Action**: Enable `CentralPackageTransitivePinningEnabled` (already enabled) and audit transitive dependencies.

**Commands**:
```bash
# List all transitive dependencies
dotnet list package --include-transitive > transitive-deps.txt

# Check for vulnerable transitive dependencies
dotnet list package --vulnerable --include-transitive

# Check for deprecated transitive dependencies
dotnet list package --deprecated --include-transitive
```

**Resolution**: Pin critical transitive dependencies explicitly in Directory.Packages.props if vulnerabilities are found.

### Task 5: Standardize Serializer Configuration

**Current Issue**: Multiple serializers in use (Newtonsoft.Json, System.Text.Json, protobuf-net, MessagePack).

**Action**: Document recommended serializer for each scenario and deprecate others.

**Recommendation Matrix**:

| Scenario | Recommended Serializer | Notes |
|----------|------------------------|-------|
| **Grain Storage (New)** | System.Text.Json | Best performance, built-in |
| **Grain Storage (Legacy)** | Newtonsoft.Json | Maintain compatibility |
| **High-Performance Binary** | protobuf-net | Fastest serialization |
| **Cross-Platform Messages** | MessagePack | Good balance |
| **Configuration Files** | System.Text.Json | Standard for .NET |
| **Grain Communication** | Orleans Built-in | Optimized for Orleans |

**Code Example**:
```csharp
// Recommended: System.Text.Json for new storage
builder.AddAzureBlobGrainStorage("MyStorage", options =>
{
    options.UseJson = true; // System.Text.Json
});

// Legacy: Newtonsoft.Json (maintain for backward compat)
builder.AddAzureBlobGrainStorage("LegacyStorage", options =>
{
    options.UseNewtonsoftJson = true;
});
```

### Task 6: Remove Unmaintained Benchmark Comparisons

**Current Issue**: Benchmarks include unmaintained serializers (Hyperion, ZeroFormatter, Utf8Json).

**Action**: Remove these serializers from benchmark projects.

**Files to Update**:
```
test/Benchmarks/Serialization/SerializationBenchmarks.cs
test/Benchmarks/Serialization/DeserializationBenchmarks.cs
```

**Changes**:
```csharp
// REMOVE these benchmark methods:
// [Benchmark] public void Hyperion_Serialize() { ... }
// [Benchmark] public void ZeroFormatter_Serialize() { ... }
// [Benchmark] public void Utf8Json_Serialize() { ... }

// KEEP these benchmark methods:
[Benchmark] public void Orleans_Serialize() { ... }
[Benchmark] public void SystemTextJson_Serialize() { ... }
[Benchmark] public void ProtobufNet_Serialize() { ... }
[Benchmark] public void MessagePack_Serialize() { ... }
```

## Migration Phases

### Phase 1: Foundation and Security (Weeks 1-2)

**Objective**: Remove security vulnerabilities and prepare infrastructure.

#### Week 1: Remove Deprecated Serializers and Set Up Automation

**Tasks**:
1. Remove Hyperion, ZeroFormatter, Utf8Json from Directory.Packages.props
2. Remove benchmark code using these serializers
3. Search codebase for any usage of these libraries
4. Set up Dependabot configuration (see CI/CD section below)
5. Set up Package Audit GitHub Action (see CI/CD section below)

**Commands**:
```bash
# Search for usage of deprecated serializers
grep -r "Hyperion" --include="*.cs" src/ test/
grep -r "ZeroFormatter" --include="*.cs" src/ test/
grep -r "Utf8Json" --include="*.cs" src/ test/

# Remove packages from Directory.Packages.props (manual edit)
# Edit: Remove <PackageVersion Include="Hyperion" ... />
# Edit: Remove <PackageVersion Include="ZeroFormatter" ... />
# Edit: Remove <PackageVersion Include="Utf8Json" ... />

# Restore and build to verify no errors
dotnet restore
dotnet build --configuration Release

# Run full test suite
dotnet test --configuration Release --no-build
```

**Success Criteria**:
- [ ] Hyperion, ZeroFormatter, Utf8Json removed from Directory.Packages.props
- [ ] No code references to removed libraries (grep returns empty)
- [ ] All tests pass
- [ ] Benchmarks run successfully without removed serializers
- [ ] Dependabot PR created (if updates available)

#### Week 2: Migrate from ZooKeeperNetEx and StructureMap

**Tasks**:
1. Add conditional compilation for ZooKeeperNetEx
2. Remove StructureMap tests or migrate to built-in DI
3. Document migration path for ZooKeeperNetEx users
4. Add deprecation warnings

**Code Changes**:

```xml
<!-- Directory.Packages.props -->
<!-- Make ZooKeeperNetEx conditional -->
<PackageVersion Include="ZooKeeperNetEx" Version="3.4.12.4" 
                Condition="'$(UseDeprecatedZooKeeper)' == 'true'" />
```

```csharp
// src/Orleans.Clustering.ZooKeeper/AssemblyInfo.cs
[assembly: System.ObsoleteAttribute(
    "ZooKeeperNetEx-based clustering is deprecated and will be removed in Orleans v10.0. " +
    "Migrate to Consul clustering or use official Apache ZooKeeper client.",
    false)]
```

```bash
# Remove StructureMap tests
rm -rf test/DependencyInjection.Tests/StructureMap/

# Update test project file
# Remove <PackageReference Include="StructureMap.Microsoft.DependencyInjection" />
```

**Success Criteria**:
- [ ] ZooKeeperNetEx only included when opt-in property is set
- [ ] StructureMap tests removed
- [ ] Deprecation warnings added to ZooKeeper clustering
- [ ] Migration guide published (see documentation section)
- [ ] All tests pass

**Rollback Plan**: Revert git commits and restore packages. Estimated rollback time: 10 minutes.

### Phase 2: Core Package Updates (Weeks 3-4)

**Objective**: Upgrade System.* and Microsoft.Extensions.* packages to 9.0.0.

#### Week 3: Update System.* Packages

**Tasks**:
1. Update `<SystemPackagesVersion>9.0.0</SystemPackagesVersion>`
2. Run `dotnet restore`
3. Fix any compilation errors
4. Run full test suite
5. Run performance benchmarks

**Commands**:
```bash
# Update Directory.Packages.props (set SystemPackagesVersion to 9.0.0)

# Restore with new versions
dotnet restore --force

# Build
dotnet build --configuration Release

# Run tests with detailed logging
dotnet test --configuration Release --no-build --logger "console;verbosity=detailed"

# Run performance benchmarks
cd test/Benchmarks
dotnet run -c Release --filter "*"
```

**Potential Issues and Mitigations**:

1. **System.Text.Json API Changes**:
   - Issue: New JSON serializer options or behavior changes
   - Mitigation: Review System.Text.Json 9.0 release notes, update serialization tests
   - Rollback: Revert to 8.0.5 if issues found

2. **System.Collections.Immutable Performance**:
   - Issue: Performance characteristics may change
   - Mitigation: Run benchmarks, compare before/after metrics
   - Rollback: Revert if performance regression >5%

**Success Criteria**:
- [ ] All System.* packages at version 9.0.0
- [ ] No compilation errors
- [ ] All tests pass (>99.5% pass rate)
- [ ] Performance benchmarks show <2% regression (acceptable)
- [ ] Memory profiling shows no significant increases

#### Week 4: Update Microsoft.Extensions.* Packages

**Tasks**:
1. Update `<MicrosoftExtensionsVersion>9.0.0</MicrosoftExtensionsVersion>`
2. Update `<MicrosoftAspNetCoreVersion>9.0.0</MicrosoftAspNetCoreVersion>`
3. Test DI registration and configuration binding
4. Test logging infrastructure
5. Test hosting and lifecycle management

**Commands**:
```bash
# Update Directory.Packages.props (set versions to 9.0.0)

# Restore
dotnet restore --force

# Build
dotnet build --configuration Release

# Run comprehensive tests
dotnet test --configuration Release --no-build --logger "trx;LogFileName=test-results.trx"

# Check for DI binding issues
dotnet test --filter "FullyQualifiedName~DependencyInjection" --verbosity normal

# Check for configuration issues
dotnet test --filter "FullyQualifiedName~Configuration" --verbosity normal
```

**Potential Issues and Mitigations**:

1. **DI Container Behavior Changes**:
   - Issue: Service resolution order or scope handling changes
   - Mitigation: Review Microsoft.Extensions.DependencyInjection 9.0 release notes
   - Test: Run all DI-related integration tests
   - Rollback: Revert to 8.0.x if breaking changes found

2. **Configuration Binding Changes**:
   - Issue: Options pattern binding behavior changes
   - Mitigation: Test all provider configurations
   - Test: Verify Azure, Redis, SQL provider startup
   - Rollback: Revert if provider initialization fails

3. **Logging Performance**:
   - Issue: Logging performance characteristics may change
   - Mitigation: Run logging benchmarks
   - Test: Verify log message allocation rates
   - Rollback: Revert if allocation increase >10%

**Success Criteria**:
- [ ] All Microsoft.Extensions.* packages at version 9.0.0
- [ ] No compilation errors or warnings
- [ ] All tests pass (>99.5% pass rate)
- [ ] DI registration works for all providers
- [ ] Configuration binding works for all scenarios
- [ ] Logging performance within acceptable range
- [ ] No breaking changes in public APIs

**Rollback Plan**: Revert Directory.Packages.props changes and restore. Estimated rollback time: 15 minutes.

### Phase 3: Azure SDK and Database Drivers (Weeks 5-6)

**Objective**: Update Azure SDK packages and database drivers to latest stable versions.

#### Week 5: Azure SDK Updates

**Tasks**:
1. Update Azure.* package versions
2. Test Azure Table Storage clustering
3. Test Azure Blob Storage persistence
4. Test Event Hubs streaming
5. Test Azure Identity authentication

**Commands**:
```bash
# Update Directory.Packages.props
# Set AzureCoreVersion, AzureStorageVersion, etc. to latest

# Restore
dotnet restore --force

# Build
dotnet build --configuration Release

# Run Azure-specific tests (requires Azure emulator or test accounts)
dotnet test --filter "FullyQualifiedName~Azure" --configuration Release

# Integration tests with Testcontainers (if applicable)
dotnet test test/Extensions/TesterAzureUtils/TesterAzureUtils.csproj
```

**Environment Setup**:
```bash
# Start Azurite for local testing
docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite

# Set connection strings
export AZURE_STORAGE_CONNECTION_STRING="UseDevelopmentStorage=true"
```

**Success Criteria**:
- [ ] Azure.Core updated to 1.46.3+
- [ ] Azure.Storage.* packages updated
- [ ] Azure.Data.Tables updated to 12.9.2+
- [ ] Azure.Messaging.EventHubs updated to 5.13.0+
- [ ] All Azure integration tests pass
- [ ] Clustering with Azure Table Storage verified
- [ ] Persistence with Azure Blob/Table verified
- [ ] Event Hubs streaming verified

#### Week 6: Database Driver Updates

**Tasks**:
1. Update Npgsql to 9.0.2
2. Update MySql.Data to 9.2.0
3. Update CassandraCSharpDriver to 3.22.0
4. Test AdoNet providers
5. Test Cassandra provider

**Commands**:
```bash
# Update Directory.Packages.props database driver versions

# Restore
dotnet restore --force

# Build
dotnet build --configuration Release

# Run database integration tests (requires test databases)
dotnet test test/Extensions/TesterAdoNet/TesterAdoNet.csproj
```

**Environment Setup**:
```bash
# Start test databases with Testcontainers (handled by tests)
# Or manually start with Docker:

# PostgreSQL
docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=password postgres:16

# MySQL
docker run -d -p 3306:3306 -e MYSQL_ROOT_PASSWORD=password mysql:8.0

# Cassandra
docker run -d -p 9042:9042 cassandra:5.0
```

**Potential Issues**:

1. **Npgsql 9.0.2 Breaking Changes**:
   - Issue: Npgsql 9.0 targets .NET 8+ and may have API changes
   - Mitigation: Review Npgsql 9.0 release notes
   - Test: Run all PostgreSQL clustering and persistence tests
   - Rollback: Revert to 8.0.5 if breaking changes affect Orleans

2. **MySql.Data 9.x Changes**:
   - Issue: MySQL connector 9.x has significant changes from 8.x
   - Mitigation: Review MySQL connector 9.0 migration guide
   - Test: Comprehensive testing of MySQL AdoNet provider
   - Alternative: Consider migrating to MySqlConnector (async, better performance)

**Success Criteria**:
- [ ] Npgsql updated to 9.0.2
- [ ] MySql.Data updated to 9.2.0 (or MySqlConnector alternative evaluated)
- [ ] CassandraCSharpDriver updated to 3.22.0
- [ ] All AdoNet tests pass for SQL Server, PostgreSQL, MySQL
- [ ] Cassandra provider tests pass
- [ ] No performance regressions in database operations
- [ ] Connection pooling working correctly

**Rollback Plan**: Revert database driver versions in Directory.Packages.props. Estimated rollback time: 10 minutes.

### Phase 4: Testing and Tooling Packages (Weeks 7-8)

**Objective**: Update testing frameworks, benchmarking tools, and development tooling.

#### Week 7: Testing Framework Updates

**Tasks**:
1. Verify xUnit 2.9.3 across all test projects
2. Update Microsoft.NET.Test.Sdk to 17.12.0
3. Update coverlet to 6.0.3
4. Update NSubstitute to 5.3.0
5. Update Verify.Xunit to 30.3.2
6. Update Testcontainers to 4.2.0

**Commands**:
```bash
# All versions should already be updated in Directory.Packages.props from Phase 2

# Run all tests to verify compatibility
dotnet test --configuration Release --no-build

# Generate code coverage report
dotnet test --configuration Release --no-build --collect:"XPlat Code Coverage"

# Run snapshot tests (Verify.Xunit)
dotnet test --filter "FullyQualifiedName~Verify" --configuration Release
```

**Potential Issues**:

1. **Testcontainers 4.x Breaking Changes**:
   - Issue: Testcontainers 4.x has API changes from 3.x
   - Mitigation: Review Testcontainers migration guide
   - Test: Run all tests using Testcontainers (database, Redis, etc.)
   - Update: Modify test setup code if necessary

2. **Verify.Xunit Snapshot Changes**:
   - Issue: Snapshot verification format may change
   - Mitigation: Review and approve new snapshots if needed
   - Test: Run all snapshot tests and verify outputs

**Success Criteria**:
- [ ] All test framework packages updated
- [ ] All tests pass (>99.5% pass rate)
- [ ] Code coverage reports generate successfully
- [ ] Testcontainers integration tests work
- [ ] Snapshot tests pass or snapshots updated appropriately

#### Week 8: Tooling and Analysis Updates

**Tasks**:
1. Update Microsoft.CodeAnalysis.* to 4.11.0
2. Update BenchmarkDotNet to 0.14.0
3. Update Microsoft.Build to 17.12.7
4. Test code generation
5. Test analyzers
6. Run benchmarks

**Commands**:
```bash
# Update Directory.Packages.props (set CodeAnalysisVersion to 4.11.0)

# Restore
dotnet restore --force

# Build
dotnet build --configuration Release

# Test code generators
dotnet test test/Orleans.CodeGenerator.Tests/ --configuration Release

# Test analyzers
dotnet test test/Analyzers.Tests/ --configuration Release

# Run benchmarks
cd test/Benchmarks
dotnet run -c Release --filter "*Serialization*"
dotnet run -c Release --filter "*Messaging*"
dotnet run -c Release --filter "*Activation*"
```

**Potential Issues**:

1. **Roslyn 4.11.0 API Changes**:
   - Issue: CodeAnalysis APIs may have breaking changes
   - Mitigation: Review Roslyn release notes
   - Test: Ensure code generators produce identical output
   - Rollback: Revert to 4.5.0 if generator issues found

2. **BenchmarkDotNet 0.14.0 Changes**:
   - Issue: Benchmark attributes or configuration may change
   - Mitigation: Review BenchmarkDotNet changelog
   - Test: Run all benchmarks and verify results are comparable

**Success Criteria**:
- [ ] Microsoft.CodeAnalysis packages updated to 4.11.0
- [ ] BenchmarkDotNet updated to 0.14.0
- [ ] Code generators produce valid code
- [ ] Analyzers work correctly (no false positives/negatives)
- [ ] All benchmarks run successfully
- [ ] Benchmark results comparable to previous versions (±5%)

**Rollback Plan**: Revert tooling package versions. Re-run code generation if necessary. Estimated rollback time: 20 minutes.

### Phase 5: Third-Party Package Updates (Weeks 9-10)

**Objective**: Update remaining third-party packages including cloud infrastructure, serialization, and utility libraries.

#### Week 9: Cloud Infrastructure and Serialization

**Tasks**:
1. Update Consul to 1.7.16.0
2. Update Google.Cloud.PubSub.V1 to 3.13.0 (stable)
3. Update Google.Protobuf to 3.29.3
4. Update protobuf-net to 3.2.45
5. Update NATS.Net to 2.7.0
6. Test all affected providers

**Commands**:
```bash
# Update Directory.Packages.props

# Restore
dotnet restore --force

# Build
dotnet build --configuration Release

# Test Consul clustering
dotnet test --filter "FullyQualifiedName~Consul" --configuration Release

# Test Google Pub/Sub streaming (if tests exist)
dotnet test --filter "FullyQualifiedName~PubSub" --configuration Release

# Test protobuf serialization
dotnet test --filter "FullyQualifiedName~Protobuf" --configuration Release
```

**Critical: Google.Cloud.PubSub.V1 Migration**

**Current**: 1.0.0-beta13 (beta, very old)
**Target**: 3.13.0 (stable, current)

This is a major version jump and may have breaking API changes.

**Migration Steps**:
1. Review Google Cloud Pub/Sub .NET client v3.x migration guide
2. Update Pub/Sub streaming provider code
3. Test thoroughly with real Pub/Sub emulator
4. Update documentation

**Code Example** (likely changes):
```csharp
// May need updates in streaming provider
// src/Orleans.Streaming.GCP/...

// Review PublisherServiceApiClient and SubscriberServiceApiClient usage
// Check for deprecated methods or changed signatures
```

**Success Criteria**:
- [ ] All cloud infrastructure packages updated
- [ ] Google.Cloud.PubSub.V1 successfully migrated to stable 3.13.0
- [ ] Consul clustering provider works
- [ ] Pub/Sub streaming provider works (if implemented)
- [ ] Protobuf serialization works
- [ ] NATS streaming works

#### Week 10: Utility Libraries and Final Updates

**Tasks**:
1. Update NodaTime to 3.2.1
2. Update FSharp.Core to 9.0.100 (if not already updated)
3. Update Autofac to 10.0.0
4. Review and update any remaining packages
5. Final comprehensive test pass

**Commands**:
```bash
# Update remaining packages in Directory.Packages.props

# Restore
dotnet restore --force

# Build
dotnet build --configuration Release

# Run FULL test suite
dotnet test --configuration Release --no-build

# Run Orleans F# serialization tests
dotnet test test/Orleans.Serialization.FSharp.Tests/

# Run DI tests with Autofac
dotnet test test/DependencyInjection.Tests/Autofac/

# Final verification
dotnet list package --outdated
dotnet list package --vulnerable
dotnet list package --deprecated
```

**Success Criteria**:
- [ ] All utility libraries updated
- [ ] NodaTime 3.2.1 working (date/time handling)
- [ ] FSharp.Core 9.0.100 working (F# interop)
- [ ] Autofac 10.0.0 DI tests passing
- [ ] No outdated packages remaining (except intentionally pinned)
- [ ] No vulnerable packages
- [ ] No deprecated packages (except scheduled for removal)
- [ ] All tests passing (>99.5%)

**Final Validation**:
```bash
# Generate comprehensive test report
dotnet test --configuration Release --logger "trx;LogFileName=final-test-results.trx" --logger "html;LogFileName=final-test-results.html"

# Generate code coverage
dotnet test --configuration Release --collect:"XPlat Code Coverage" --results-directory ./coverage

# Run performance benchmarks and compare to baseline
cd test/Benchmarks
dotnet run -c Release --filter "*" --exporters json
# Compare results with baseline saved before Phase 1
```

**Rollback Plan**: Full rollback involves reverting Directory.Packages.props to pre-modernization state. Estimated rollback time: 30 minutes + full test suite run.

## Migration Roadmap Timeline

### Visual Timeline

```
Week 1-2:   [Foundation & Security]
            ├─ Remove deprecated serializers
            ├─ Deprecate ZooKeeperNetEx
            └─ Set up automation (Dependabot, Package Audit)

Week 3-4:   [Core Package Updates]
            ├─ System.* → 9.0.0
            └─ Microsoft.Extensions.* → 9.0.0

Week 5-6:   [Cloud & Database Updates]
            ├─ Azure SDK packages
            └─ Database drivers (Npgsql, MySql.Data, Cassandra)

Week 7-8:   [Testing & Tooling]
            ├─ xUnit, coverlet, NSubstitute
            ├─ Roslyn 4.11.0
            └─ BenchmarkDotNet 0.14.0

Week 9-10:  [Third-Party & Finalization]
            ├─ Google.Cloud.PubSub.V1 → 3.13.0
            ├─ Consul, NATS, protobuf-net
            ├─ Utility libraries
            └─ Final comprehensive validation

Post-Week 10: [Continuous Monitoring]
              ├─ Dependabot PRs reviewed weekly
              ├─ Package audit runs automated
              └─ Quarterly dependency reviews
```

### Milestones and Success Criteria

| Milestone | Week | Success Criteria | Owner |
|-----------|------|------------------|-------|
| **M1: Security Cleanup** | 2 | Deprecated packages removed, automation configured | Security Lead |
| **M2: .NET 9 Alignment** | 4 | System & Microsoft packages at 9.0.0, all tests pass | Technical Lead |
| **M3: Provider Modernization** | 6 | Azure SDK & DB drivers updated, integration tests pass | Infrastructure Lead |
| **M4: Tooling Update** | 8 | CodeAnalysis & testing tools updated, benchmarks pass | DevOps Lead |
| **M5: Complete Modernization** | 10 | All packages updated, comprehensive validation complete | Project Manager |

## CI/CD Automation

### Dependabot Configuration

Create `.github/dependabot.yml`:

```yaml
version: 2
updates:
  # NuGet package updates
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
      day: "monday"
      time: "09:00"
      timezone: "America/Los_Angeles"
    open-pull-requests-limit: 10
    reviewers:
      - "orleans-maintainers"
    assignees:
      - "dependency-manager"
    labels:
      - "dependencies"
      - "automated"
    commit-message:
      prefix: "deps"
      include: "scope"
    # Grouping strategy for related packages
    groups:
      microsoft-extensions:
        patterns:
          - "Microsoft.Extensions.*"
        update-types:
          - "minor"
          - "patch"
      azure-sdk:
        patterns:
          - "Azure.*"
        update-types:
          - "minor"
          - "patch"
      system-packages:
        patterns:
          - "System.*"
        update-types:
          - "minor"
          - "patch"
      testing-packages:
        patterns:
          - "xunit*"
          - "coverlet.*"
          - "Microsoft.NET.Test.Sdk"
        update-types:
          - "minor"
          - "patch"
    # Ignore specific packages
    ignore:
      # Packages scheduled for removal
      - dependency-name: "Hyperion"
      - dependency-name: "ZeroFormatter"
      - dependency-name: "Utf8Json"
      - dependency-name: "ZooKeeperNetEx"
      - dependency-name: "StructureMap.Microsoft.DependencyInjection"
      # Packages with known issues (temporary)
      # - dependency-name: "Some.Package"
      #   versions: ["x.y.z"]

  # GitHub Actions updates
  - package-ecosystem: "github-actions"
    directory: "/"
    schedule:
      interval: "monthly"
    open-pull-requests-limit: 5
    reviewers:
      - "devops-team"
    labels:
      - "github-actions"
      - "automated"
```

### Package Audit Workflow

Create `.github/workflows/package-audit.yml`:

```yaml
name: Package Security and Health Audit

on:
  schedule:
    # Run every Monday at 9 AM UTC
    - cron: '0 9 * * 1'
  workflow_dispatch:
  pull_request:
    paths:
      - 'Directory.Packages.props'
      - '**/*.csproj'
      - '.github/workflows/package-audit.yml'

jobs:
  audit:
    name: Package Audit
    runs-on: ubuntu-latest
    permissions:
      contents: read
      issues: write
      pull-requests: write
    
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Restore packages
        run: dotnet restore
      
      - name: Check for vulnerable packages
        id: vulnerable
        continue-on-error: true
        run: |
          echo "## Vulnerable Packages" > vulnerable-packages.md
          dotnet list package --vulnerable --include-transitive >> vulnerable-packages.md
          cat vulnerable-packages.md
      
      - name: Check for deprecated packages
        id: deprecated
        continue-on-error: true
        run: |
          echo "## Deprecated Packages" > deprecated-packages.md
          dotnet list package --deprecated >> deprecated-packages.md
          cat deprecated-packages.md
      
      - name: Check for outdated packages
        id: outdated
        continue-on-error: true
        run: |
          echo "## Outdated Packages" > outdated-packages.md
          dotnet list package --outdated >> outdated-packages.md
          cat outdated-packages.md
      
      - name: Create audit report
        if: always()
        run: |
          echo "# Package Audit Report - $(date '+%Y-%m-%d')" > audit-report.md
          echo "" >> audit-report.md
          cat vulnerable-packages.md >> audit-report.md
          echo "" >> audit-report.md
          cat deprecated-packages.md >> audit-report.md
          echo "" >> audit-report.md
          cat outdated-packages.md >> audit-report.md
          
          cat audit-report.md
      
      - name: Upload audit report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: package-audit-report
          path: audit-report.md
          retention-days: 90
      
      - name: Create issue for vulnerabilities
        if: steps.vulnerable.outcome == 'failure'
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            const report = fs.readFileSync('audit-report.md', 'utf8');
            
            const issues = await github.rest.issues.listForRepo({
              owner: context.repo.owner,
              repo: context.repo.repo,
              labels: 'security,dependencies,automated',
              state: 'open'
            });
            
            if (issues.data.length === 0) {
              await github.rest.issues.create({
                owner: context.repo.owner,
                repo: context.repo.repo,
                title: '🚨 Package Security Vulnerabilities Detected',
                body: report,
                labels: ['security', 'dependencies', 'automated', 'high-priority']
              });
            }
      
      - name: Comment on PR
        if: github.event_name == 'pull_request'
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            const report = fs.readFileSync('audit-report.md', 'utf8');
            
            await github.rest.issues.createComment({
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number: context.issue.number,
              body: report
            });

  license-check:
    name: License Compliance Check
    runs-on: ubuntu-latest
    
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Install dotnet-project-licenses
        run: dotnet tool install --global dotnet-project-licenses
      
      - name: Generate license report
        run: |
          dotnet-project-licenses --input . --output-directory licenses --export-license-texts --format json
          dotnet-project-licenses --input . --output-directory licenses --format markdown
      
      - name: Check for incompatible licenses
        run: |
          # Check for GPL, AGPL, or other incompatible licenses
          if grep -iE "(GPL|AGPL|SSPL)" licenses/licenses.json; then
            echo "::error::Incompatible license detected!"
            exit 1
          fi
      
      - name: Upload license report
        uses: actions/upload-artifact@v4
        with:
          name: license-report
          path: licenses/
          retention-days: 90
```

### Package Update Validation Workflow

Create `.github/workflows/package-update-validation.yml`:

```yaml
name: Package Update Validation

on:
  pull_request:
    paths:
      - 'Directory.Packages.props'
    types: [opened, synchronize, reopened]

jobs:
  validate-update:
    name: Validate Package Updates
    runs-on: ubuntu-latest
    
    steps:
      - name: Checkout PR branch
        uses: actions/checkout@v4
        with:
          ref: ${{ github.event.pull_request.head.sha }}
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Restore packages
        run: dotnet restore
      
      - name: Build solution
        run: dotnet build --configuration Release --no-restore
      
      - name: Run unit tests
        run: dotnet test --configuration Release --no-build --logger "trx;LogFileName=test-results.trx"
      
      - name: Check for test failures
        if: failure()
        run: |
          echo "::error::Tests failed after package update. Please investigate."
          exit 1
      
      - name: Run integration tests (fast subset)
        run: |
          # Run a subset of fast integration tests
          dotnet test test/DefaultCluster.Tests --configuration Release --no-build --filter "Category!=LongRunning"
      
      - name: Comment on PR with results
        if: always()
        uses: actions/github-script@v7
        with:
          script: |
            const testResults = "Test results will be added here"; // Parse from trx file
            await github.rest.issues.createComment({
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number: context.issue.number,
              body: `## Package Update Validation Results\n\n${testResults}`
            });

  benchmark-comparison:
    name: Performance Benchmark Comparison
    runs-on: ubuntu-latest
    if: contains(github.event.pull_request.labels.*.name, 'performance-impact')
    
    steps:
      - name: Checkout base branch
        uses: actions/checkout@v4
        with:
          ref: ${{ github.event.pull_request.base.sha }}
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Run baseline benchmarks
        run: |
          cd test/Benchmarks
          dotnet run -c Release --filter "*Serialization*" --exporters json
          mv BenchmarkDotNet.Artifacts/results/*-report.json baseline-results.json
      
      - name: Checkout PR branch
        uses: actions/checkout@v4
        with:
          ref: ${{ github.event.pull_request.head.sha }}
          clean: false
      
      - name: Run PR benchmarks
        run: |
          cd test/Benchmarks
          dotnet run -c Release --filter "*Serialization*" --exporters json
          mv BenchmarkDotNet.Artifacts/results/*-report.json pr-results.json
      
      - name: Compare benchmarks
        run: |
          # Compare baseline and PR results
          # Post comment if regression > 5%
          echo "Benchmark comparison will be implemented here"
```

### Automated Dependency Update Policy

Configure GitHub branch protection rules:

```yaml
# Branch protection for main/master
required_status_checks:
  - "Package Audit"
  - "Package Update Validation"
  - "Build"
  - "Tests"

# Auto-merge policy for low-risk Dependabot PRs
auto-merge:
  enabled: true
  conditions:
    - label: "dependencies"
    - label: "auto-merge-safe"
    - all_checks_passed: true
    - update_types: ["version-update:semver-patch"]
```

## Risk Assessment and Rollback Strategy

### Risk Matrix

| Risk Category | Probability | Impact | Mitigation | Rollback Time |
|---------------|-------------|--------|------------|---------------|
| **Breaking API Changes** | Medium | High | Comprehensive testing, staged rollout | 15-30 minutes |
| **Performance Regression** | Low | High | Benchmark comparison, gradual deployment | 15 minutes |
| **Security Vulnerability Introduction** | Low | Critical | Pre-update security scan, audit workflow | 10 minutes |
| **Database Driver Incompatibility** | Medium | High | Testcontainers validation, staging environment testing | 20 minutes |
| **Serialization Breaking Changes** | Low | High | Serialization compatibility tests, version pinning | 15 minutes |
| **Test Infrastructure Failures** | Medium | Medium | Parallel test execution, test isolation | 30 minutes |
| **Build System Issues** | Low | Medium | CI/CD validation, local build verification | 20 minutes |

### Rollback Procedures

#### Quick Rollback (Individual Phase)

If issues are detected during a specific phase:

```bash
# 1. Identify the problematic phase
git log --oneline --grep="Phase X"

# 2. Revert the Directory.Packages.props changes
git revert <commit-hash-of-phase-x>

# 3. Restore packages
dotnet restore --force

# 4. Build and verify
dotnet build --configuration Release
dotnet test --configuration Release --no-build

# 5. Deploy if verification passes
```

**Time Estimate**: 15-30 minutes depending on phase complexity.

#### Full Rollback (All Changes)

If multiple phases need to be reverted:

```bash
# 1. Create rollback branch
git checkout -b rollback-dependency-modernization

# 2. Revert Directory.Packages.props to pre-modernization state
git checkout origin/main -- Directory.Packages.props

# 3. Restore and build
dotnet restore --force
dotnet build --configuration Release

# 4. Run full test suite
dotnet test --configuration Release --no-build --logger "trx"

# 5. If tests pass, merge rollback branch to main
git commit -m "Rollback: Revert dependency modernization due to [REASON]"
git push origin rollback-dependency-modernization

# 6. Create PR and fast-track merge
```

**Time Estimate**: 1-2 hours (includes testing and deployment).

### Validation Checklist

Before proceeding to the next phase:

- [ ] All unit tests pass (>99.5% pass rate)
- [ ] All integration tests pass (>95% pass rate, known flaky tests excluded)
- [ ] Performance benchmarks within acceptable range (±5%)
- [ ] Memory profiling shows no significant leaks or increases
- [ ] Build time has not increased significantly (<10%)
- [ ] No new compiler warnings introduced
- [ ] Documentation updated if API changes occurred
- [ ] Changelog entry created
- [ ] Stakeholders notified of progress

## Conclusion and Next Steps

This comprehensive dependency modernization plan provides a structured, risk-managed approach to updating the Orleans-4316 NuGet packages. By following the phased timeline, implementing robust CI/CD automation, and maintaining clear rollback strategies, the Orleans project will achieve:

1. **Enhanced Security**: All known vulnerabilities eliminated, deprecated packages removed
2. **.NET 9 Alignment**: Full compatibility with modern .NET features and performance improvements
3. **Sustainable Maintenance**: Automated dependency monitoring and update workflows
4. **Improved Developer Experience**: Clear, organized package management structure
5. **Production Stability**: Comprehensive testing and validation at each phase

### Immediate Next Steps

1. **Week 0 (Pre-Phase 1 Preparation)**:
   - Review and approve this modernization plan
   - Assign owners for each phase (see milestones table)
   - Set up communication channels for modernization progress
   - Create tracking issue on GitHub with links to each phase milestone
   - Schedule kick-off meeting with all stakeholders

2. **Week 1 (Begin Phase 1)**:
   - Create feature branch: `feature/dependency-modernization`
   - Set up Dependabot configuration
   - Set up Package Audit GitHub Action
   - Remove Hyperion, ZeroFormatter, Utf8Json
   - Communicate deprecation plan to community

3. **Ongoing (Throughout All Phases)**:
   - Weekly status updates to stakeholders
   - Monitor CI/CD pipeline for any issues
   - Respond to community feedback on deprecations
   - Update documentation as changes are made

### Long-Term Maintenance

After completion of the 10-week modernization:

- **Quarterly Dependency Reviews**: Review all packages for updates, security issues, and deprecations
- **Automated Dependabot PRs**: Review and merge low-risk updates weekly
- **Annual Major Version Planning**: Plan major version updates (e.g., .NET 10) well in advance
- **Community Engagement**: Solicit feedback on deprecated packages and migration paths
- **Documentation Maintenance**: Keep dependency documentation up-to-date

By executing this plan systematically and maintaining ongoing vigilance through automation, the Orleans project will maintain a modern, secure, and high-performance dependency ecosystem well into the future.

Task completed: Created comprehensive NuGet dependency audit (06-dependency-audit.md) and modernization plan (07-package-modernization-plan.md) documentation for Orleans-4316 with actionable steps, automation strategies, and phased migration roadmap.
