# Build Quality and Reproducibility

This repository uses a shared build policy so local development, CI validation, container builds, template packaging, and scaffolded consumer output follow the same baseline expectations.

## SDK Policy

The repository pins the .NET SDK through `global.json`.

Current policy:

```json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

The pinned SDK feature band keeps local and CI builds aligned. `latestPatch` allows patch-level SDK servicing updates within the selected feature band without silently moving to a newer feature band.

CI workflows use `actions/setup-dotnet` with `global-json-file: global.json` so the repository SDK policy remains the single source of truth.

## Central Package Management

NuGet package versions are centralized in `Directory.Packages.props` by enabling:

```xml
<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
```

Project files should reference packages without inline `Version` attributes unless a documented exception is required.

This keeps package drift visible, makes dependency review easier, and ensures scaffolded consumer output can restore with the same package policy as the source repository.

## NuGet Lock Files

`RestorePackagesWithLockFile` is enabled for every project. Each project therefore keeps a `packages.lock.json` beside its project file, including projects emitted by the `dotnet new` template.

Repository, generated-scaffold, and Docker restores use `dotnet restore --locked-mode`. CI first verifies that every project has a lock file so a missing file cannot be silently generated during a supposedly locked restore. After an intentional dependency change, run `dotnet restore --force-evaluate`, review the lock-file diff, and commit it with the dependency update.

## Shared Build Properties

Common build settings live in `Directory.Build.props`.

The shared policy includes:

- `TargetFramework` set to `net10.0`.
- Nullable reference types enabled.
- Implicit usings enabled.
- Centralized version metadata.
- Deterministic builds.
- Embedded debug symbols.
- Repository metadata for Source Link.
- .NET analyzers enabled.
- Code style enforcement during build.
- XML documentation generation.

## Deterministic Builds

The build policy enables deterministic output where appropriate:

```xml
<Deterministic>true</Deterministic>
<ContinuousIntegrationBuild Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
<DebugType>embedded</DebugType>
```

CI also passes `/p:ContinuousIntegrationBuild=true` during release builds, template packing, scaffolded output validation, documentation builds, and container publish builds.

## Source Link

Source Link support is configured with repository metadata and the `Microsoft.SourceLink.GitHub` package.

Project files that produce assemblies should include:

```xml
<PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="all" />
```

The CI checkout uses full history with `fetch-depth: 0` for build jobs that need repository metadata.

## Analyzer and Formatting Policy

Analyzer severity and code-style preferences are defined in `.editorconfig`.

CI enforces formatting with:

```powershell
dotnet format ./NetCoreApplicationTemplate.slnx --verify-no-changes --verbosity minimal
```

Analyzer settings are intentionally production-oriented but not configured as global warnings-as-errors. This allows the template to remain practical while still surfacing reliability, security, performance, usage, code-quality, and style issues during builds and IDE development.

## Release Build Quality Gates

Before a release, the expected validation path is:

```powershell
dotnet build ./NetCoreApplicationTemplate.slnx --configuration Release --no-restore /p:ContinuousIntegrationBuild=true
dotnet format ./NetCoreApplicationTemplate.slnx --verify-no-changes --verbosity minimal
dotnet test ./NetCoreApplicationTemplate.slnx --configuration Release --no-build --verbosity normal /p:ContinuousIntegrationBuild=true
dotnet pack ./NetCoreApplicationTemplate.Template.csproj --configuration Release --output ./artifacts/template-package /p:ContinuousIntegrationBuild=true
```

The CI workflow additionally:

- Runs dependency review on pull requests.
- Generates coverage reports.
- Enforces the configured global and security-critical coverage thresholds.
- Runs CodeQL analysis.
- Packs and installs the template package.
- Scaffolds a consumer project.
- Verifies expected scaffolded files and excluded maintainer files.
- Builds and tests scaffolded output on Linux, Windows, and macOS.

## Scaffolded Output

The generated template intentionally includes:

- `.editorconfig`
- `global.json`
- `Directory.Build.props`
- `Directory.Packages.props`

These files are part of the consumer build contract because package versions and build quality settings are centralized at the repository root.

## Coverage Policy

The CI coverage gate is intentionally held at 75% for v1.0.2 as a minimum safety net. Contract-level integration tests protect advertised runtime behavior directly, while the global threshold prevents broad coverage regression without forcing low-value tests.

Security-critical files also have a stricter file-level coverage gate. This second gate exists because global line coverage can hide concentrated risk in files responsible for error handling, request classification, identity resolution, audit attribution, security headers, forwarded headers, rate limiting, and persistence normalization.

The protected file list is maintained in:

```text
eng/security-critical-coverage.json
```

The CI assertion script is:

```text
eng/Assert-SecurityCriticalCoverage.ps1
```

The per-file gate evaluates ReportGenerator's generated Cobertura output and fails CI with actionable file-level messages when a protected file falls below its configured line or branch threshold.

Protected files should be added when they control security, trust boundaries, request identity, safe failure, audit attribution, or persistence safety. Lowering a protected threshold should be treated as a quality-gate change and should include a reason in the pull request.


## Dependency Upgrade Policy

Dependency updates should be reviewed by impact level.

| Update Type | Review Expectation |
|:------------|:-------------------|
| Security updates | Review and merge promptly after CI passes unless the update causes a documented compatibility break. |
| Patch updates | Prefer merging after CI, template smoke tests, and dependency review pass. |
| Minor updates | Review release notes, then merge after CI and smoke tests pass. |
| Major updates | Treat as compatibility work. Review release notes, migration guides, runtime behavior, and scaffolded output before merging. |
| Runtime-sensitive updates | Manually review authentication, EF Core, middleware, logging, telemetry, container, and workflow dependencies even for patch or minor updates. |

Dependabot groups related dependencies where practical, but grouped dependency pull requests still require human review before merging.

## Version and Release Notes

The repository version is centralized in build metadata. Release notes should identify any change that affects:

- SDK feature-band policy.
- Target framework.
- Central package management.
- Analyzer severity.
- CI quality gates.
- Template scaffolded build files.
- Dependency policy.

After `v1.0.0`, changes to these items should be treated as release-surface changes because they can affect downstream generated applications.
