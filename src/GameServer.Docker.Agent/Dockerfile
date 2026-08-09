FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Run as root to access Docker socket
# The Docker socket requires root or docker group permissions
USER root
WORKDIR /app
EXPOSE 8080

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION_NUMBER=0.0.1.0
WORKDIR /src
COPY ["GameServer.Docker.Agent.csproj", "."]
RUN dotnet restore "./GameServer.Docker.Agent.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./GameServer.Docker.Agent.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:Version=$VERSION_NUMBER

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GameServer.Docker.Agent.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=$VERSION_NUMBER

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Add health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "GameServer.Docker.Agent.dll"]