# Docker Development Workflow

The application includes optional Docker support for local development, container validation, and release artifact publication.

Docker support provides a repeatable local runtime environment. It does not replace normal `dotnet restore`, `dotnet build`, or `dotnet test` workflows.

## Prerequisites

Install Docker Desktop or another Docker-compatible container runtime.

## Build the Image

From the repository root:

```powershell
docker build -t projecttemplate-web:dev .
```

The Dockerfile uses a multi-stage build:

- `mcr.microsoft.com/dotnet/sdk:10.0` for restore and publish.
- `mcr.microsoft.com/dotnet/aspnet:10.0` for the runtime image.

The runtime image runs as the built-in non-root .NET container user and exposes port `8080`.

## Run with Docker

```powershell
docker run --rm -p 8080:8080 projecttemplate-web:dev
```

The application is available at:

```text
http://localhost:8080
```

## Run with Docker Compose

```powershell
docker compose up --build
```

Stop the container:

```powershell
docker compose down
```

Remove local Docker volumes:

```powershell
docker compose down -v
```

## Local Environment Overrides

The compose file can load an optional `.env` file for local overrides.

Start from the checked-in example file:

```powershell
Copy-Item .env.example .env
```

The `.env` file is intentionally ignored by Git. Do not commit local secrets, machine-specific values, or production configuration.

The default Compose values are suitable for local development. Production deployments should provide environment-specific settings through the hosting platform, secret store, or deployment pipeline.

## Local SQLite Data

The compose file stores the local SQLite database under a named Docker volume mounted to:

```text
/app/data
```

The compose file overrides the default SQLite connection string:

```text
ConnectionStrings__ApplicationDatabase=Data Source=/app/data/application-dev.db
```

This keeps generated container data out of the repository working tree.

## Logs

The compose file mounts application logs to a named Docker volume mounted to:

```text
/app/Logs
```

View container logs:

```powershell
docker compose logs -f projecttemplate.web
```

## Forwarded Headers

The application already supports forwarded headers through the `ProjectTemplate:ForwardedHeaders` configuration section.

For local Docker development, the compose file enables forwarded headers and clears the default known proxy/network restrictions so forwarded headers can be tested from local container or proxy scenarios.

This setting is for local development only:

```text
ProjectTemplate__ForwardedHeaders__ClearKnownNetworksAndProxies=true
```

Production deployments should prefer explicit trusted proxy settings:

```json
"ProjectTemplate": {
  "ForwardedHeaders": {
    "Enabled": true,
    "ForwardLimit": 1,
    "KnownProxies": [
      "10.0.0.10"
    ],
    "KnownNetworks": [
      "10.0.0.0/24"
    ],
    "AllowedHosts": [
      "example.com"
    ]
  }
}
```

## Health Probe Contract

The container image listens on port `8080`.

Health endpoints are available at:

```text
http://localhost:8080/health
http://localhost:8080/health/ready
http://localhost:8080/health/live
```

The Dockerfile intentionally delegates active HTTP health probing to Docker Compose, Kubernetes, load balancers, or hosting infrastructure rather than adding probe-only tools such as curl or wget to the runtime image.

Recommended probe use:

| Path | Use |
|:-----|:----|
| `/health/live` | Liveness probe. Confirms the application process can respond. |
| `/health/ready` | Readiness probe. Reserved for dependency-aware checks tagged `ready`. |
| `/health` | General health endpoint for simple infrastructure checks. |

The baseline readiness endpoint does not include database or external service checks until those checks are explicitly registered and tagged for readiness.

## Forwarded Header Smoke Test

Start the container:

```powershell
docker compose up --build
```

Send a request with forwarded headers:

```powershell
curl -I http://localhost:8080/ `
  -H "X-Forwarded-For: 203.0.113.10" `
  -H "X-Forwarded-Proto: https"
```

Then review the application logs:

```powershell
docker compose logs projecttemplate.web
```

The request should complete successfully, and forwarded header handling should not break normal local routing.

## Container Image Strategy

The local development image tag is:

```text
projecttemplate-web:dev
```

This tag is intended for local development and testing. It is not the published production image contract.

The repository release workflow publishes tag-driven images to:

```text
ghcr.io/AsiBackbone/netcoreapplicationtemplate
```

See [Container Release Publishing](container-publish.md) for the GHCR publish, scan, SBOM, signing, and provenance workflow.

Applications generated from the template may adapt the Dockerfile for production deployment. Production consumers may pin image digests, use organization-approved base images, or apply additional hardening based on their deployment requirements.

Stable-release changes to documented image names, tag conventions, exposed ports, or runtime image strategy should be reviewed as release-surface changes.

See [ADR-0003: Record Release Surface and Distribution Strategy](../adr/0003-record-release-surface-and-distribution-strategy.md) for the release-surface decision.

## Notes

Do not commit generated SQLite databases, local logs, container volumes, secrets, or environment-specific credentials.
