# NetCoreApplicationTemplate 2.11.11 Release Notes

2.11.11 is a patch release for the .NET 10 template. It services framework and observability dependencies, strengthens
restore isolation, and improves release and documentation validation without moving the template to .NET 11.

## Restore and supply-chain safeguards

The repository and generated scaffold now use NuGet package source mapping. Every current package ID is explicitly
routed to NuGet.org, preventing an unlisted machine-level or user-level feed from satisfying those dependencies.
Applications that add a private feed must add an equally explicit package mapping for it.

The pinned .NET 10 SDK and ASP.NET Core runtime container image digests are refreshed. The image tags, target
framework, Dockerfile structure, and intended runtime behavior are unchanged.

Dependency-check validation now carries exact package-, version-, and CVE-scoped suppressions for OpenTelemetry
findings that originate in unrelated Go and JavaScript components. These suppressions do not classify a vulnerability
in the referenced .NET packages as acceptable; they document scanner fingerprint mismatches and expire on
2027-02-28 for mandatory review.

## Dependency servicing

The .NET 10 framework-coupled dependencies are serviced without crossing the framework boundary:

* Entity Framework Core packages are updated from 10.0.14 to 10.0.15.
* ASP.NET Core authentication and testing packages and Microsoft.Extensions configuration packages are updated from
  10.0.14 to 10.0.15.
* OpenTelemetry packages are updated to 1.21.0.
* AspNet.Security.OAuth.GitHub is updated from 10.0.0 to 10.1.0.
* System.Security.Cryptography.Pkcs is updated from 10.0.13 to 10.0.14.
* Microsoft.SourceLink.GitHub, Microsoft.NET.Test.Sdk, xunit.v3, and ReportGenerator receive maintenance updates.

Dependabot now ignores major-version updates for framework-coupled Microsoft packages, preserving the supported
.NET 10 baseline while continuing to propose compatible servicing updates.

## Release and documentation reliability

Built assemblies again use the SDK-default informational version format, `<version>+<commit>`, while Source Link
continues to record repository provenance. Documentation validation now checks the published release references and
uses a dedicated, retry-bounded Zenodo DOI check instead of depending on Lychee's response to the archive service.

## Compatibility

This is a backward-compatible patch release for the feature-complete 2.x line. The package ID, template identities,
short name, supported options, public application surface, and `net10.0` target remain unchanged.

## Updating

Install the template with `dotnet new install NetCoreApplicationTemplate::2.11.11`. Updating the template package
affects future scaffolds; existing generated applications retain their current dependencies and restore configuration
until updated separately.

See the [changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md) for the complete
change list.
