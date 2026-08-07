# ADR-0003: Record Release Surface and Distribution Strategy

## Status

Accepted

## Context

The template is being prepared for a future `v1.0.0` release. After `1.0.0`, version numbers need to communicate compatibility expectations for developers who install, clone, scaffold, or maintain applications from the template.

This ADR records the release-readiness decisions needed before that stable release:

- What parts of the template are part of the SemVer public surface.
- How future changes should be classified as MAJOR, MINOR, or PATCH.
- Whether the stable distribution model is package installation or clone-and-modify.
- Which API versioning scheme is the default.
- Which container base image strategy the template follows.
- How repository metadata should cross-link related public works.

## Decision

Use this ADR as the release-readiness decision record for SemVer, template distribution, API versioning, container image strategy, and repository metadata cross-linking.

The concrete `v1.0.0` consumer contract is defined in [`docs/articles/public-surface-v1.md`](../articles/public-surface-v1.md). This ADR records the governing decision; the public-surface article lists the committed identifiers, generated structure, configuration keys, endpoint conventions, middleware invariants, and publishing conventions.

### Semantic Versioning Public Surface

The public SemVer surface includes template behavior that consumers are expected to rely on after `v1.0.0`:

- Generated project and folder layout.
- Template identity, short name, parameters, symbols, and source-name replacement behavior.
- Documented configuration key names and documented default values.
- Default API route conventions and API versioning behavior.
- Default middleware ordering and documented pipeline extension points.
- Documented container build and tag conventions.
- Public or protected class signatures documented for use by generated applications.
- Published documentation, release notes, and repository metadata that describe release behavior.

Internal implementation details remain changeable unless they are documented as extension points, generated into consumer applications, or required by customization guidance.

### Change Classification

Use the following rules after `v1.0.0`.

| Area | MAJOR | MINOR | PATCH |
|:---|:---|:---|:---|
| Scaffolded project layout | Rename, remove, or relocate generated projects, folders, files, namespaces, or startup entry points in a breaking way. | Add optional projects, examples, folders, or files without breaking existing assumptions. | Correct comments, formatting, documentation, or non-breaking file contents. |
| Template parameters and symbols | Rename, remove, invert, or materially change parameters, symbols, short names, or source-name replacement behavior. | Add optional parameters or symbols with safe defaults. | Fix parameter descriptions, constraints, or packaging metadata without behavior changes. |
| Configuration key names and defaults | Rename or remove documented keys, change value types, or change defaults in a way that materially changes runtime behavior. | Add optional keys, supported values, or provider-specific settings with compatible defaults. | Correct examples, validation messages, docs, or comments without changing documented behavior. |
| Default API route conventions | Change or remove documented route templates, default API version behavior, or canonical versioning conventions. | Add optional route forms, endpoint examples, or supported API versions while preserving existing routes. | Fix route docs, examples, or tests without changing route behavior. |
| Default middleware ordering | Reorder, remove, or replace documented middleware in a way that changes routing, error-handling, forwarded-header, or authorization behavior. | Add optional middleware or new documented extension points without breaking the existing order. | Fix ordering docs, tests, or narrow bugs while preserving the documented pipeline contract. |
| Container image tag conventions | Rename, remove, or redefine documented image names, tag forms, exposed ports, or supported runtime image family. | Add supported tags, image variants, or examples while preserving documented defaults. | Rebuild against compatible patch images or fix Docker docs without changing behavior. |
| Internal class signatures | Change documented public or protected signatures used by generated applications, extension points, tests, or examples in a breaking way. | Add optional overloads, extension methods, or helpers without breaking consumers. | Refactor private or internal details that are not documented consumer extension points. |

When a change is ambiguous, classify it by the effect on a consumer who already generated an application from the previous stable version. If their documented build, configuration, route usage, deployment script, or customization path breaks, treat the change as breaking unless a clear migration path exists.

### Template Distribution Model

The stable release direction is a published NuGet template package installable with `dotnet new install`.

Expected stable usage:

```powershell
dotnet new install <published-template-package-id>
dotnet new netcoreapp-template -n ContosoSecurityPortal
```

Clone-and-modify remains valid for source review, contribution, and preview use. Until the published package exists, documentation may continue to describe local installation from the repository root:

```powershell
dotnet new install ./
```

Documentation should clearly distinguish local preview installation from the intended stable package distribution path.

### Default API Versioning Scheme

Use URL segment API versioning as the canonical default for scaffolded API routes.

Default route convention:

```text
/api/v{version}/<resource>
```

The template also supports `X-API-Version` request header versioning as a secondary compatibility option.

Default behavior:

- Default API version: `1.0`.
- Canonical style: URL segment versioning.
- Optional compatibility style: `X-API-Version` request header.
- Assume default version when unspecified: enabled.
- Report API versions: enabled.

URL segment versioning is the default because it is visible in documentation, logs, browser testing, curl examples, and reverse proxy traces. Header versioning remains available when a consuming application prefers stable resource URLs.

### Container Base Image Strategy

Use Microsoft .NET container images with a multi-stage Dockerfile:

- `mcr.microsoft.com/dotnet/sdk:<major.minor>` for restore, build, and publish stages.
- `mcr.microsoft.com/dotnet/aspnet:<major.minor>` for the final runtime image.
- A non-root runtime user where supported by the selected .NET image.
- A documented default HTTP container port.

The documented local development image tag remains:

```text
projecttemplate-web:dev
```

If this repository later publishes reusable container images, release tags should align with repository release tags:

```text
vMAJOR.MINOR.PATCH
vMAJOR.MINOR
vMAJOR
```

Production consumers may pin image digests or use organization-approved base images in generated applications when their deployment policies require it.

### Repository Metadata Cross-Link Strategy

Repository metadata may cross-link related SSRN works when the relationship is described as related conceptual work, not as a software dependency or proof claim.

Use these relationship meanings:

- `isSupplementTo` when repository metadata points to a related conceptual paper or framework that the repository supplements as an applied software artifact.
- `isSupplementedBy` when an external paper or metadata record points back to the repository as a companion artifact.
- `references` when repository documentation explicitly cites a paper for background context.

The repository should not be described as implementing the Eden Hypothesis or ASI Backbone. It may be described as a practical software-architecture artifact that can be discussed alongside those works when the relationship is framed as conceptual, architectural, or illustrative.

## Consequences

Positive consequences:

- Future maintainers have a single release-surface reference before `v1.0.0`.
- Template consumers receive clearer compatibility expectations.
- Documentation can link to one ADR for versioning, packaging, API versioning, containers, and metadata decisions.
- Later issues can classify breaking and non-breaking work more consistently.

Trade-offs and risks:

- The ADR creates a stronger maintenance obligation after `v1.0.0`.
- Documentation can make otherwise internal details part of the practical public surface.
- Package distribution is the intended stable direction, but local installation remains necessary until package publishing is implemented.
- Supporting both URL segment and header API versioning requires documentation clarity.
- Container tag guidance may need revision if the repository later publishes official images.

## Alternatives Considered

- Treat the repository as clone-and-modify only: simpler, but weaker as a reusable template release.
- Record separate ADRs for each decision: more granular, but harder to use for the consolidated `v1.0.0` readiness decision set.
- Defer SemVer classification until package publication: avoids early commitment, but leaves readiness work without a decision baseline.
- Use header-only API versioning: keeps URLs stable, but makes examples and troubleshooting less obvious.
- Use custom container base images immediately: possible for specific organizations, but too opinionated for a public template baseline.
- Avoid SSRN/repository metadata cross-linking entirely: safest from an overclaiming perspective, but less useful for artifact discovery when the relationship is carefully bounded.

## Related References

- [Issue #140](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/140)
- [Issue #138](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/138)
- [`docs/articles/template-packaging.md`](../articles/template-packaging.md)
- [`docs/articles/api-versioning.md`](../articles/api-versioning.md)
- [`docs/articles/docker.md`](../articles/docker.md)
- [`docs/articles/deployment.md`](../articles/deployment.md)
- [`README.md`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/README.md)
- [`CITATION.cff`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CITATION.cff)
- [`docs/articles/public-surface-v1.md`](../articles/public-surface-v1.md)
- [`docs/articles/v1-migration-guide.md`](../articles/v1-migration-guide.md)
- [`RELEASE.md`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/RELEASE.md)
- [`CHANGELOG.md`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md)
