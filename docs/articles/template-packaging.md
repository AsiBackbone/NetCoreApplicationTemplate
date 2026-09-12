# Template Packaging

This repository includes a `dotnet new` template scaffold and a NuGet template package project.

The scaffold can be installed locally from source for development, or packed into a `.nupkg` and installed through the same package-based flow expected for consumers.

Package-based validation is preferred because it verifies the actual distribution artifact instead of only the repository working tree.

## Template Identity

| Field | Value |
|:---|:---|
| Template short name | `netcoreapp-template` |
| Current NuGet package ID | `NetCoreApplicationTemplate` |
| Previous NuGet package ID | `CDCavell.NetCoreApplicationTemplate` |
| Template identity | `AsiBackbone.NetCoreApplicationTemplate.CSharp` |
| Template group identity | `AsiBackbone.NetCoreApplicationTemplate` |
| Source replacement token | `ProjectTemplate` |
| Current package version | `2.9.0` |

The `2.0.0` release moved the public NuGet package ID to `NetCoreApplicationTemplate`. Release `2.8.0` aligns the internal template identity and group identity with the `AsiBackbone` organization namespace. The public package ID and template short name remain unchanged.

## Consumer Scaffold Boundaries

The scaffolded output intentionally includes:

- Source projects under `src/`.
- Baseline tests under `tests/`.
- Checked-in `packages.lock.json` files for every generated project.
- The shared AsiBackbone `.editorconfig` formatting and analyzer baseline.
- Docker support files.
- `LICENSE.txt`.
- `ASSETS-LICENSES.md`.
- A consumer-oriented `README.md` generated from `.template.content/README.md`.

The repository-level `ASSETS-LICENSES.md` inventories package, documentation, NuGet icon, and generated-application assets. Generated projects receive `.template.content/ASSETS-LICENSES.md`, which retains the applicable package and application-favicon notices without referring to repository-only `docs/images/` or `PACKAGE-ICON.png` paths. Update both inventories when generated dependencies change; update the repository inventory when repository or package image assets change.

The scaffolded output intentionally excludes repository-maintainer content such as:

- `.github/` workflow and issue-template files.
- `.template.config/` and `.template.content/` authoring files.
- DocFX documentation source and ADRs.
- Changelog, citation, community, governance, contribution, security, and release-management files.
- Repository maintenance scripts and pre-generated SQL migration scripts.
- Repository maintainer badges and release instructions.

The scaffold does not ship a pre-generated SQL migration script. SQL is provider-specific and a checked-in generated artifact can become stale as migrations evolve. Consumers should generate and review a script from the migrations in their generated application for the target provider and deployment state.

### Generated Formatting Baseline

The repository-root `.editorconfig` is the shared baseline ratified for `AsiBackbone/AsiBackbone`, `AsiBackbone/Learning`, and `AsiBackbone/NetCoreApplicationTemplate`. It is explicitly included by `.template.config/template.json`, packed into the template NuGet package, and required by the golden scaffold manifest. The scaffold validator verifies that the generated file matches the repository baseline exactly, including for all six supported authentication and database option combinations exercised by CI.

Generated applications receive this file as a consistent starting point and may customize it after generation to suit their own requirements. The baseline currently has no NetCoreApplicationTemplate-specific extensions. Any future repository-only exception must be clearly labeled, scoped to template-authoring paths that cannot match generated application content, and reviewed before the baseline is synchronized to sibling repositories.

## Template Content Overlay

`.template.content/` holds files that replace their `src/` counterparts during scaffolding. An overlay file shadows the repository file entirely, so a setting added to `src/ProjectTemplate.Web/appsettings.json` and not to the overlay is absent from every generated project. Nothing fails when that happens: the overlay is still valid JSON, and the missing key falls back to its code default, so the drift is only visible by comparing the two files.

`eng/Validate-TemplateContentOverlay.ps1` compares the JSON key structure of each overlay file against its `src` counterpart and fails when a key exists in one and not the other. Values are compared as well, except where the overlay holds a template token such as `TemplateAuthEnabled`, `TemplateDataProvider`, or `TemplateDataConnectionStringName`, since those are substituted during scaffolding and are expected to differ.

CI runs it on every build. Run it locally after changing either file:

```powershell
./eng/Validate-TemplateContentOverlay.ps1
```

Overlay files with no `src` counterpart, such as `nuget.config` and `README.md`, are scaffold-specific by design and are skipped.

## Golden Scaffold Manifest

The approved default scaffold surface is tracked in `eng/scaffold-manifest.default.json`.

The manifest is validated by `eng/Validate-ScaffoldManifest.ps1` after CI packs the template package, installs the generated `.nupkg`, and scaffolds the default `ContosoSecurityPortal` project.

The manifest check fails when:

- An expected consumer file is missing.
- An expected consumer directory is missing.
- An unexpected root-level file is generated.
- The generated `.editorconfig` differs from the shared repository baseline.
- A maintainer-only path such as `.github/`, `.template.config/`, `.template.content/`, `docs/`, `eng/`, `scripts/`, `CHANGELOG.md`, `CITATION.cff`, `CONTRIBUTING.md`, `RELEASE.md`, or `SECURITY.md` appears in the scaffolded output.
- The generated consumer README contains repository maintainer content such as workflow badges or the current-release block.

The manifest intentionally allows recursive content under `src/` and `tests/` because those folders are part of the consumer scaffold surface. Root-level additions should be added to `expectedFiles` only when they are intended public scaffold files.

### Validate a Generated Scaffold Locally

After packing and installing the template package, generate the default scaffold:

```powershell
dotnet new netcoreapp-template -n ContosoSecurityPortal --output ./artifacts/scaffold/ContosoSecurityPortal
```

Validate the scaffold against the checked-in manifest:

```powershell
./eng/Validate-ScaffoldManifest.ps1 -ScaffoldRoot ./artifacts/scaffold/ContosoSecurityPortal
```

### Intentionally Update the Manifest

When the public scaffold surface intentionally changes, regenerate the scaffold from the packed `.nupkg`, inspect the generated output, and then refresh the manifest:

```powershell
./eng/Validate-ScaffoldManifest.ps1 -ScaffoldRoot ./artifacts/scaffold/ContosoSecurityPortal -Generate
```

Review the manifest diff carefully before committing. Changes to root-level files, maintainer-only exclusions, template source boundaries, README content checks, or public scaffold folders should be treated as release-surface changes.

## Pack the Template Package

From the repository root:

```powershell
dotnet pack ./NetCoreApplicationTemplate.Template.csproj --configuration Release --output ./artifacts/template-package
```

Tagged releases generate an SPDX SBOM and a SHA-256 manifest from this exact `.nupkg`, publish GitHub build-provenance attestations, and attach the durable evidence to the corresponding GitHub Release. NuGet Trusted Publishing authenticates the protected workflow to NuGet.org but does not sign the package. NuGet author signing remains deferred under [ADR-0005](../adr/0005-defer-nuget-package-signing.md); the OCI image's Cosign signature is a separate control and does not cover the `.nupkg`.

See [Container Release Publishing](container-publish.md#durable-release-assets) for the release asset contract and tested consumer verification commands.

## Install the Template Package

Install the published package from NuGet:

```powershell
dotnet new install NetCoreApplicationTemplate::2.9.0
```

Install a locally packed package:

```powershell
dotnet new install ./artifacts/template-package/NetCoreApplicationTemplate.2.9.0.nupkg
```

## Create a New Project from the Template

From a separate working directory:

```powershell
dotnet new netcoreapp-template -n ContosoSecurityPortal
```

Use a project name that is also a valid C# identifier, such as `ContosoSecurityPortal`. Dotted project names require additional template symbol handling so namespace replacement and type-name replacement can be handled separately.

This creates a new project using `ContosoSecurityPortal` as the replacement name for the source template namespace and project prefix.

## Template Options

The template intentionally exposes a small set of stable options for common scaffold variants.

| Option | Default | Supported values | Description |
|:---|:---|:---|:---|
| `--authProvider` | `cookie` | `cookie`, `none` | Selects the generated authentication baseline. Use `cookie` for the default cookie-authentication-ready baseline or `none` to generate the application with application authentication disabled by default. |
| `--dbProvider` | `sqlite` | `sqlite`, `sqlserver`, `none` | Selects the generated data access mode. Use `sqlite` for the default local development configuration, `sqlserver` for the SQL Server provider configuration, or `none` to generate the application with EF Core data access disabled. |
| `--skipRestore` | `false` | `true`, `false` | Skips the post-create NuGet restore action when set to `true`. |

Example non-default scaffold:

```powershell
dotnet new netcoreapp-template `
  --name ContosoNoAuthSqlServer `
  --authProvider none `
  --dbProvider sqlserver
```

All supported variants preserve the template's core infrastructure guardrails, including structured logging, centralized error handling, health checks, security headers, rate limiting, and safe defaults.

### Authentication-disabled variant

The `--authProvider none` option generates the application with `ProjectTemplate:Authentication:Enabled` and `ProjectTemplate:Authentication:Cookie:Enabled` set to `false`.

The application still includes the authentication and authorization infrastructure so consumers can enable or replace authentication later. Test cases that intentionally exercise protected endpoints may enable test authentication through in-memory test configuration.

### SQL Server variant

The `--dbProvider sqlserver` option excludes the SQLite-specific `Data/Migrations` history and migration tests from generated output. Consumers must create provider-compatible SQL Server migrations before running `dotnet ef database update`.

### Data-access-disabled variant

The `--dbProvider none` option generates the application with `ProjectTemplate:DataAccess:Provider` set to `None`.

When data access is disabled, EF Core application data access services are not registered, including `ApplicationDbContext`, `IDbContextFactory<ApplicationDbContext>`, and EF-backed services that require `ApplicationDbContext`.

This mode is appropriate for lightweight applications, workers, external modules, or services that use a separate persistence strategy.

## Restore, Build, and Test the Generated Project

```powershell
cd ContosoSecurityPortal
dotnet restore --locked-mode
dotnet build --configuration Release
dotnet test --configuration Release
```

Every generated project includes a checked-in `packages.lock.json`. Use `--locked-mode` in CI and other repeatable builds so restore fails when declared dependencies and the recorded graph differ. After an intentional dependency change, run `dotnet restore --force-evaluate`, review the resulting lock-file changes, and commit them with the package update.

## Update the Installed Template

Install the newer package version:

```powershell
dotnet new install NetCoreApplicationTemplate
```

The .NET SDK updates the installed template package when the package identity matches and the new package version is higher.

## Uninstall the Template

```powershell
dotnet new uninstall NetCoreApplicationTemplate
```

## Local Repository Install

For local authoring and quick iteration, the template can still be installed from the repository root.

On Windows:

```powershell
dotnet new install .\
```

On Linux or macOS:

```bash
dotnet new install ./
```

Then generate from a separate working directory:

```powershell
dotnet new netcoreapp-template -n ContosoSecurityPortal
```

Local repository install is useful during template development, but package-based install should remain the primary validation path before release.

## CI Smoke Test

The CI workflow packs the template package, installs the generated `.nupkg`, scaffolds a new project with `dotnet new netcoreapp-template`, validates the scaffolded output and its required lock files against the golden manifest, restores in locked mode, builds the generated output, runs generated tests, and uninstalls the template package.

The smoke test runs on Linux, Windows, and macOS so path handling and package install behavior are validated across supported runner environments.

On Linux runners, CI also validates the Docker consumer path from the generated scaffolded output. The Docker restore layer copies each required project lock file before running `dotnet restore --locked-mode`. This Docker smoke test builds the generated Docker image, validates `docker compose config`, starts the generated Compose application, verifies `/health/live`, captures Compose logs for diagnostics, and tears down the Compose stack during cleanup.

Docker runtime validation is intentionally limited to Linux runners. The goal is to prove that Docker files emitted by the template are usable by a generated consumer project, not to certify Docker host behavior across every operating system.

## Distribution Direction

The stable distribution model is a published NuGet template package installable with `dotnet new install`.

Stable usage follows this pattern:

```powershell
dotnet new install NetCoreApplicationTemplate
dotnet new netcoreapp-template -n ContosoSecurityPortal
```
