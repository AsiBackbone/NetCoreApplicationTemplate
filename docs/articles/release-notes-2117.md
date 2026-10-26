# NetCoreApplicationTemplate 2.11.7 Release Notes

Release date: 2026-10-25

NetCoreApplicationTemplate 2.11.7 is a backward-compatible maintenance release
for the feature-complete 2.x line. It services Microsoft .NET dependencies,
refreshes package locks, keeps the reviewed dependency-check suppression aligned
with the pinned OpenTelemetry package, and updates repository validation for the
.NET 10 Microsoft Testing Platform command line. The package ID, template
identity, short name, supported template options, public application surface,
generated behavior, and `net10.0` target are unchanged.

## Dependency servicing

- Updated Microsoft ASP.NET Core, Entity Framework Core, and Extensions packages
  from 10.0.13 to 10.0.14.
- Updated `Microsoft.SourceLink.GitHub` from 10.0.402 to 10.0.403.
- Refreshed the infrastructure, Web, and test-project package lock files.

The servicing update does not add a package dependency, template option, or
generated application capability.

## Dependency-check suppression maintenance

The narrowly scoped OWASP Dependency-Check suppression for CVE-2026-54285 now
matches the pinned `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.20.1 NuGet
package. That CVE applies to the JavaScript `@opentelemetry/core` package rather
than OpenTelemetry .NET. Obsolete OpenTelemetry and Source Link suppressions were
removed.

Suppression validation now requires every exact NuGet package and version in the
suppression file to match `Directory.Packages.props`. A future dependency update
therefore requires an explicit suppression review instead of silently retaining
stale entries. Other packages and vulnerabilities remain scanned.

## Repository validation compatibility

The .NET 10 Microsoft Testing Platform command line requires a solution to be
identified with `--solution`. Scaffold smoke and option-matrix jobs now invoke:

```text
dotnet test --solution ./ContosoSecurityPortal.slnx
```

This preserves the existing test scope across Linux, Windows, and macOS. It does
not alter generated projects, the selected Microsoft Testing Platform runner, or
application runtime behavior.

## Upgrade actions

Installing this template version does not modify applications generated from an
earlier release. Existing applications may adopt the serviced package versions
and refreshed lock files independently after their normal compatibility and
security validation.

## Release verification

The release candidate is validated with version and tag consistency, locked
restore, Release builds and tests, formatting, template overlay, option-matrix
and lock-file checks, dependency suppression and release-evidence tests,
scaffold-manifest validation, package metadata and content checks, SBOM
generation, isolated package installation, generated-project builds and tests,
an EF Core migration generation and rebuild, and strict documentation
validation. Hosted CI, template smoke tests, and the non-publishing
template-package workflow provide the remaining cross-platform, option-matrix,
security, container, and release-artifact evidence. Stable publication occurs
only from the protected `v2.11.7` tag after the preparation commit is merged to
`main` and CI has completed on that commit.

See also:

- [Changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md)
- [2.11.6 release notes](release-notes-2116.md)
- [Template packaging](template-packaging.md)
- [Build quality and reproducibility](build-quality.md)
- [Repository security profile](repository-security-profile.md)
