# Limes.Web — multi-stage build for the Container App (browser UI + API).
# Build context is the repo root (see azure.yaml docker.context).

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore against the referenced project graph first for better layer caching.
COPY Limes.sln ./
COPY src/Limes.Core/Limes.Core.csproj src/Limes.Core/
COPY src/Limes.Agents/Limes.Agents.csproj src/Limes.Agents/
COPY src/Limes.Web/Limes.Web.csproj src/Limes.Web/
RUN dotnet restore src/Limes.Web/Limes.Web.csproj

# Copy the rest and publish. samples/ and knowledge/ are required: the sample intake and
# the Minerva grounding corpus are embedded as resources (see Limes.Web.csproj).
COPY src/ src/
COPY samples/ samples/
COPY knowledge/ knowledge/
RUN dotnet publish src/Limes.Web/Limes.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
# Listen on 80 so the real image and the first-provision placeholder share the ingress port.
ENV ASPNETCORE_HTTP_PORTS=80
EXPOSE 80
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Limes.Web.dll"]
