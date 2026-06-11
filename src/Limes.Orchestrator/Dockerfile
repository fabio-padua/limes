# Limes orchestrator — multi-stage build for the Container Apps Job.
# Build context is the repo root (see azure.yaml docker.context).

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore against the full project graph first for better layer caching.
COPY Limes.sln ./
COPY src/Limes.Core/Limes.Core.csproj src/Limes.Core/
COPY src/Limes.Agents/Limes.Agents.csproj src/Limes.Agents/
COPY src/Limes.Orchestrator/Limes.Orchestrator.csproj src/Limes.Orchestrator/
RUN dotnet restore src/Limes.Orchestrator/Limes.Orchestrator.csproj

# Copy the rest and publish a framework-dependent, trimmed-context build.
COPY src/ src/
COPY knowledge/ knowledge/
RUN dotnet publish src/Limes.Orchestrator/Limes.Orchestrator.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app ./
# Ship the curated grounding corpus so agents mode can self-ground out of the box.
COPY --from=build /src/knowledge/ ./knowledge/
ENTRYPOINT ["dotnet", "Limes.Orchestrator.dll"]
