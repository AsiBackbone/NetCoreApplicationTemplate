# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:83e0db97c45d2e39b80123fe42940a23c423405a17f80b608a4b8768033d6392 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:57460add89e2b3dd1950c41d8b7dc96eeb7a24d13d98e3656ce9997a8b746bd6 AS final
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
