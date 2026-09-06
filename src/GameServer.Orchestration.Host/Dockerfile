FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION_NUMBER=1.0.0.0
WORKDIR /src

# Restore — copy csproj files first for layer caching
COPY ["./src/GameServer.Contracts/GameServer.Contracts.csproj", "GameServer.Contracts/"]
COPY ["./src/GameServer.Orchestration/GameServer.Orchestration.csproj", "GameServer.Orchestration/"]
COPY ["./src/GameServer.Orchestration.Host/GameServer.Orchestration.Host.csproj", "GameServer.Orchestration.Host/"]

RUN dotnet restore "GameServer.Orchestration.Host/GameServer.Orchestration.Host.csproj"

# Copy source
COPY ["./src/GameServer.Contracts/", "GameServer.Contracts/"]
COPY ["./src/GameServer.Orchestration/", "GameServer.Orchestration/"]
COPY ["./src/GameServer.Orchestration.Host/", "GameServer.Orchestration.Host/"]

WORKDIR "/src/GameServer.Orchestration.Host"
RUN dotnet build "GameServer.Orchestration.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:Version=$VERSION_NUMBER

# This stage publishes the Worker service
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GameServer.Orchestration.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=$VERSION_NUMBER

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameServer.Orchestration.Host.dll"]
