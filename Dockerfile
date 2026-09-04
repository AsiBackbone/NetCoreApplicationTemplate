# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.400@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["src/ProjectTemplate.Infrastructure/ProjectTemplate.Infrastructure.csproj", "src/ProjectTemplate.Infrastructure/"]
COPY ["src/ProjectTemplate.Infrastructure/packages.lock.json", "src/ProjectTemplate.Infrastructure/"]
COPY ["src/ProjectTemplate.Web/ProjectTemplate.Web.csproj", "src/ProjectTemplate.Web/"]
COPY ["src/ProjectTemplate.Web/packages.lock.json", "src/ProjectTemplate.Web/"]

RUN dotnet restore "src/ProjectTemplate.Web/ProjectTemplate.Web.csproj" --locked-mode

COPY . .

RUN dotnet publish "src/ProjectTemplate.Web/ProjectTemplate.Web.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish .

RUN mkdir -p /app/Logs /app/data /app/data-protection-keys && chown -R $APP_UID:$APP_UID /app

USER $APP_UID

# Health probing is delegated to Docker Compose, Kubernetes, load balancers,
# or hosting infrastructure. Use /health/live and /health/ready.
HEALTHCHECK NONE

ENTRYPOINT ["dotnet", "ProjectTemplate.Web.dll"]
