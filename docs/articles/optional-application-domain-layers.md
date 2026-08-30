# Application and Domain Extension Boundaries

The generated .NET Core Application Template intentionally starts with a compact solution. This page documents the **NCAT-specific extension boundary** for consumers who want to add projects such as an application layer, domain layer, or additional infrastructure modules.

General guidance about when additional layers are justified, dependency-direction tradeoffs, CQRS, MediatR, DDD, and incremental migration now lives in the organization-wide Learning site:

> [Growing Beyond a Simple Application Structure](https://asibackbone.github.io/Learning/architecture/growing-beyond-a-simple-application-structure.html)

NCAT does **not** require Clean Architecture, CQRS, MediatR, DDD, or any particular layer count. The generated structure is a working baseline, not a universal architecture prescription.

## What NCAT Generates Today

The default generated solution contains three projects:

```text
src/
├── ProjectTemplate.Web/
└── ProjectTemplate.Infrastructure/

tests/
└── ProjectTemplate.Web.Tests/
```

The generated solution file contains the same projects:

```text
ProjectTemplate.Web
ProjectTemplate.Infrastructure
ProjectTemplate.Web.Tests
```

Their current responsibilities are:

| Project | NCAT responsibility |
| --- | --- |
| `ProjectTemplate.Web` | ASP.NET Core host, composition root, middleware pipeline, authentication and authorization registration, endpoints, UI/API concerns, configuration, logging, telemetry, and runtime startup. |
| `ProjectTemplate.Infrastructure` | EF Core data access and other infrastructure/persistence implementation details that should not live directly in the web host. |
| `ProjectTemplate.Web.Tests` | Automated validation of startup, configuration, middleware, authentication, authorization, data access wiring, error handling, and related generated behavior. |

See [Project Structure](project-structure.md) for the complete repository and generated-solution layout.

## Intentional Dependency Direction

The generated project-reference direction is deliberately small:

```text
ProjectTemplate.Web
        |
        v
ProjectTemplate.Infrastructure

ProjectTemplate.Web.Tests
        |
        v
ProjectTemplate.Web
```

Today:

- `ProjectTemplate.Web.csproj` references `ProjectTemplate.Infrastructure.csproj`.
- `ProjectTemplate.Infrastructure.csproj` has no project reference back to `Web`.
- `ProjectTemplate.Web.Tests.csproj` references `ProjectTemplate.Web.csproj`.

Keep `Web` as the composition root unless the application deliberately adopts a different host/composition model. Avoid introducing a reference from `Infrastructure` back to `Web`.

## Where Additional Projects Can Be Inserted

A generated application may add projects under `src/` when its own complexity justifies them. For example:

```text
src/
├── ProjectTemplate.Web/
├── ProjectTemplate.Application/
├── ProjectTemplate.Domain/
└── ProjectTemplate.Infrastructure/
```

One possible dependency shape is:

```text
ProjectTemplate.Web
        |
        +----> ProjectTemplate.Application
        |
        +----> ProjectTemplate.Infrastructure

ProjectTemplate.Application
        |
        v
ProjectTemplate.Domain

ProjectTemplate.Infrastructure
        |
        +----> ProjectTemplate.Application   (when implementing application-owned ports)
        |
        +----> ProjectTemplate.Domain        (when infrastructure needs domain types)
```

That is only an example. Consumers may use application services without a separate Domain project, add bounded-context projects, split infrastructure by provider, or keep the generated two-project runtime structure.

The template does not require a mediator, command/query handlers, repositories, domain events, or a particular DDD model.

## Consumer Changes After Generation

If you have already generated an application and are adding projects only to that application, the NCAT template-authoring files do not need to change.

At minimum, update the generated application itself:

1. Add the new `.csproj` file under the desired source or test folder.
2. Add the project to the generated `.slnx`.
3. Add only the project references required by the dependency direction you chose.
4. Register new application/infrastructure services from the `Web` composition root.
5. Add focused tests for meaningful new behavior.
6. Run restore, build, and test for the complete generated solution.

If the new projects require NuGet packages and the generated application retains NCAT's central package-management files, add package versions through `Directory.Packages.props` rather than scattering version numbers across project files.

## Changing NCAT's Generated Default

Contributors changing the **template itself** so every future scaffold contains another project must update more than a solution file.

### Solution and project references

Update:

- `NetCoreApplicationTemplate.slnx` so the source template solution contains the new project.
- The relevant `.csproj` files so project-reference direction matches the intended generated architecture.
- Tests when the new boundary changes startup composition or runtime behavior.

The template uses `ProjectTemplate` as its source replacement token, so project names, namespaces, and paths intended for generated output should continue to use that token consistently.

### Template metadata

Review `.template.config/template.json`.

Its current `primaryOutputs` explicitly list:

- `ProjectTemplate.slnx`
- `src/ProjectTemplate.Infrastructure/ProjectTemplate.Infrastructure.csproj`
- `src/ProjectTemplate.Web/ProjectTemplate.Web.csproj`
- `tests/ProjectTemplate.Web.Tests/ProjectTemplate.Web.Tests.csproj`

If a new generated project is part of the template's primary output surface, add it there.

The template source rules already include `src/**/*` and `tests/**/*`, so files placed under those trees are generally within the packaged template source. Still verify the generated package and scaffold rather than assuming a new project is emitted correctly.

### Golden scaffold manifest

Review `eng/scaffold-manifest.default.json`.

The default manifest explicitly records the expected generated project files and directories. If the default scaffold intentionally gains a project, update the manifest only after generating and inspecting the packed template output.

Use the existing validator:

```powershell
./eng/Validate-ScaffoldManifest.ps1 `
  -ScaffoldRoot ./artifacts/scaffold/ContosoSecurityPortal
```

When the change is intentional, regenerate the manifest with `-Generate`, then review the diff before committing.

### Package project

Review `NetCoreApplicationTemplate.Template.csproj`.

The package project currently packs `src/**/*` and `tests/**/*`, which normally captures additional projects placed beneath those roots. A new layer therefore may not require a new explicit `Content` entry, but package contents still need validation.

If a new generated artifact lives outside the existing included roots, the package project and template source rules may both require changes.

### Tests and CI

A template-structure change should continue to pass:

- Repository restore.
- Release build.
- Repository tests.
- Template package creation.
- Package installation.
- Default scaffold generation.
- Golden scaffold-manifest validation.
- Build and test of the generated scaffold.

The existing template smoke test exercises the packed `.nupkg` rather than only the repository working tree. Preserve that distinction when validating structural changes.

## NCAT Invariants to Preserve

Adding layers should not accidentally weaken the baseline that NCAT is meant to provide.

Unless a change intentionally revises the template contract and updates its tests/documentation, preserve these characteristics:

- `Web` remains the ASP.NET Core composition root.
- Middleware ordering remains centralized and deliberate.
- Structured logging and telemetry remain wired through the host.
- Centralized error handling and Problem Details remain the application failure boundary.
- Security headers, forwarded-header handling, rate limiting, health checks, authentication, and authorization remain explicit infrastructure concerns.
- Data-access provider selection remains configuration driven.
- `Infrastructure` does not depend on `Web`.
- Generated projects continue to build and test as one solution.
- Template renaming through the `ProjectTemplate` source token continues to work.
- The default scaffold manifest reflects the actual consumer-facing output.
- Package-based smoke validation remains authoritative for the distributable template.

Additional projects should extend these boundaries rather than silently bypass them.

## Architecture Guidance Lives in Learning

The question **"Should this application add another layer at all?"** is intentionally outside NCAT's product-reference scope.

For general architecture education covering:

- when a simple application structure is enough;
- signals that justify additional boundaries;
- dependency-direction reasoning;
- optional Application and Domain projects;
- CQRS, MediatR, and DDD tradeoffs;
- incremental migration strategies; and
- the cost of premature layering;

see [Growing Beyond a Simple Application Structure](https://asibackbone.github.io/Learning/architecture/growing-beyond-a-simple-application-structure.html).

That Learning article treats NCAT as one concrete reference implementation among many possible application structures. This page remains authoritative only for **how NCAT is generated and what must be considered when extending its template boundary**.
