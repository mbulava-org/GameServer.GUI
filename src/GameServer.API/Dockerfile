# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER 0
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION_NUMBER=1.0.0.0
WORKDIR /src
COPY ["./src/GameServer.Contracts/GameServer.Contracts.csproj", "GameServer.Contracts/"]
COPY ["./src/GameServer.Catalog/GameServer.Catalog.csproj", "GameServer.Catalog/"]
COPY ["./src/GameServer.Orchestration/GameServer.Orchestration.csproj", "GameServer.Orchestration/"]
COPY ["./src/GameServer.Deployment/GameServer.Deployment.csproj", "GameServer.Deployment/"]
COPY ["./src/GameServer.Monitoring/GameServer.Monitoring.csproj", "GameServer.Monitoring/"]
COPY ["./src/GameServer.API/GameServer.API.csproj", "GameServer.API/"]

RUN dotnet restore "GameServer.API/GameServer.API.csproj"

COPY ["./src/GameServer.Contracts/", "GameServer.Contracts/"]
COPY ["./src/GameServer.Catalog/", "GameServer.Catalog/"]
COPY ["./src/GameServer.Orchestration/", "GameServer.Orchestration/"]
COPY ["./src/GameServer.Deployment/", "GameServer.Deployment/"]
COPY ["./src/GameServer.Monitoring/", "GameServer.Monitoring/"]
COPY ["./src/GameServer.API/", "GameServer.API/"]

WORKDIR "/src/GameServer.API"
RUN dotnet build "GameServer.API.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:Version=$VERSION_NUMBER

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GameServer.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=$VERSION_NUMBER

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameServer.API.dll"]