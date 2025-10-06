# syntax=docker/dockerfile:1

# Multi-stage build for Orleans dev silo (targeting .NET 8)
# Environment variables (can be overridden at build/run time):
# - ASPNETCORE_URLS: Kestrel binding, default to http://0.0.0.0:8080
# - ORLEANS_SILO_PORT: Silo-to-silo port (default 11111)
# - ORLEANS_GATEWAY_PORT: Client gateway port (default 30000)
# - ORLEANS_DASHBOARD_PORT: Orleans dashboard port if enabled (default 8080, served by ASP.NET host)
# - ORLEANS_CLUSTER_ID: Orleans ClusterId (default dev)
# - ORLEANS_SERVICE_ID: Orleans ServiceId (default dev-service)

ARG TARGETFRAMEWORK=net8.0
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:8.0
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:8.0

# Build stage
FROM ${SDK_IMAGE} AS build
WORKDIR /src

# Restore: copy solution and props to enable restore
COPY Orleans.slnx ./
COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY Directory.Packages.props ./
COPY global.json ./

# Copy only the playground cluster host we will run in container (ActivationRebalancing.Cluster)
# Note: This is a small, single-process Orleans silo host suitable for local dev.
COPY playground/ActivationRebalancing/ActivationRebalancing.Cluster/ ./playground/ActivationRebalancing/ActivationRebalancing.Cluster/
# Also copy any project references it may rely on via relative paths (no-op if not needed)
# If additional projects are needed, dotnet restore will request them during restore below.

# Restore dependencies
RUN dotnet restore ./playground/ActivationRebalancing/ActivationRebalancing.Cluster/ActivationRebalancing.Cluster.csproj

# Publish
RUN dotnet publish ./playground/ActivationRebalancing/ActivationRebalancing.Cluster/ActivationRebalancing.Cluster.csproj -c Release -o /app/publish -r linux-x64 --self-contained false /p:PublishReadyToRun=true

# Runtime stage
FROM ${RUNTIME_IMAGE} AS final

# Create non-root user
#  - Use well-known non-root uid/gid
RUN addgroup --system appgroup && adduser --system --ingroup appgroup --home /home/appuser appuser
USER appuser

WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ORLEANS_SILO_PORT=11111 \
    ORLEANS_GATEWAY_PORT=30000 \
    ORLEANS_DASHBOARD_PORT=8080 \
    ORLEANS_CLUSTER_ID=dev \
    ORLEANS_SERVICE_ID=dev-service

# Copy published app
COPY --from=build /app/publish/ ./

# Expose common Orleans ports:
# 11111 - silo-to-silo
# 30000 - client gateway
# 8080  - HTTP endpoints (health, dashboard if enabled)
EXPOSE 11111 30000 8080

# For hosts which respect ASPNETCORE_URLS, no explicit args are needed.
# If the host does not use Kestrel, the ports are still exposed for Orleans networking.
ENTRYPOINT ["dotnet", "ActivationRebalancing.Cluster.dll"]
