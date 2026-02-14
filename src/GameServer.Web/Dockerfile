# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION_NUMBER=1.0.0.0

WORKDIR /src
COPY ["./src/GameServer.Web/GameServer.Web.csproj", "GameServer.Web/"]
COPY ["./src/GameServer.Docker.Client/GameServer.Docker.Client.csproj", "GameServer.Docker.Client/"]
COPY ["./src/GameServer.Docker.Client/nswag.json", "GameServer.Docker.Client/"]
COPY ["./src/GameServer.Docker/GameServer.Docker.csproj", "GameServer.Docker/"]

RUN dotnet restore "GameServer.Docker/GameServer.Docker.csproj"	
RUN dotnet restore "GameServer.Docker.Client/GameServer.Docker.Client.csproj"	
RUN dotnet restore "GameServer.Web/GameServer.Web.csproj"

COPY ["./src/GameServer.Web/", "GameServer.Web/"]
COPY ["./src/GameServer.Docker.Client/", "GameServer.Docker.Client/"]
COPY ["./src/GameServer.Docker/", "GameServer.Docker/"]

# Build GameServer.Docker first (required for NSwag code generation)
RUN dotnet build "GameServer.Docker/GameServer.Docker.csproj" -c $BUILD_CONFIGURATION -o /app/build/docker /p:Version=$VERSION_NUMBER

# Build GameServer.Docker.Client (NSwag will generate client code from GameServer.Docker API)
RUN dotnet build "GameServer.Docker.Client/GameServer.Docker.Client.csproj" -c $BUILD_CONFIGURATION -o /app/build/client /p:Version=$VERSION_NUMBER

# Build GameServer.Web (final application)
RUN dotnet build "GameServer.Web/GameServer.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:Version=$VERSION_NUMBER

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GameServer.Web/GameServer.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=$VERSION_NUMBER

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameServer.Web.dll"]