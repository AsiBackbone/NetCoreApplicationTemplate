# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.404@sha256:8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:aac50ceef10391e78a899e3d2ccd00bbafa2dcb56ccf4586cbccf00edb84fc21 AS final
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
