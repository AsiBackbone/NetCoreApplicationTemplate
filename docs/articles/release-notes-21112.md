# NetCoreApplicationTemplate 2.11.12 Release Notes

2.11.12 is a patch release for the .NET 10 template. It services SAML authentication and test dependencies, refreshes
the pinned .NET 10 container images, and strengthens repository assurance and package-boundary validation without
moving the template to .NET 11.

## Dependency and container servicing

`Sustainsys.Saml2.AspNetCore2` is updated from 2.11.0 to 2.11.1. Its `Microsoft.IdentityModel` SAML dependencies move
from 5.2.4 to 5.7.0, and the affected lock files are refreshed. `coverlet.MTP` is updated to 10.1.1, while repository
coverage reporting moves to ReportGenerator 5.6.1.

The pinned .NET 10 SDK and ASP.NET Core runtime container image digests are refreshed. The image tags, target
framework, Dockerfile structure, and intended runtime behavior are unchanged.

## Repository assurance and release reliability

Three read-only assurance agent profiles now provide bounded security, release, and quality review guidance. They are
advisory only: they cannot edit files, execute commands, access secrets or external systems, approve changes, merge,
publish, deploy, or create releases. Maintainers retain all consequential authority.

A regression-tested governance validator checks those boundaries in repository content, the packed template, and the
generated scaffold. This protects the distribution boundary so maintainer-only governance material is not emitted to
template consumers. CI restore and build commands also explicitly target the solution, avoiding ambiguity with the
template-packaging project at the repository root.

The repository contribution documentation now describes controlled contribution and pull-request approval, and the
organization proposal guidance records that proposals may be openly submitted while downstream authority remains
bounded to human maintainers. These are repository-governance changes and do not alter generated applications.

Pinned GitHub Actions receive maintenance updates, including CodeQL 4.39.5, Cosign Installer 4.1.4, and zizmor 0.9.2.
Actions remain pinned to immutable commit SHAs.

## Compatibility

This is a backward-compatible patch release for the feature-complete 2.x line. The package ID, template identities,
short name, supported options, public application surface, and `net10.0` target remain unchanged. Existing generated
applications are not modified automatically.

## Updating

Install the template with `dotnet new install NetCoreApplicationTemplate::2.11.12`. Updating the template package
affects future scaffolds; existing generated applications retain their current dependencies and container digests until
updated separately.

See the [changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md) for the complete
change list.
