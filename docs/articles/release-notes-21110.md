# NetCoreApplicationTemplate 2.11.10 Release Notes

2.11.10 is a patch release for the .NET 10 template. It strengthens release-artifact verification and updates OpenTelemetry instrumentation dependencies.

## Signed release artifacts

The release workflow now signs the exact `.nupkg` attached to the GitHub Release with Sigstore keyless signing. Publication verifies the package bundle against the protected release workflow identity before the package is pushed to NuGet.org. The container workflow also verifies the published image signature by immutable digest and checks its GitHub provenance.

Successful package and container checks now explicitly return a successful process exit code after exercising their required negative cases. This prevents a valid tag-triggered release from stopping after all signature assertions have passed.

The GitHub Release package and its `*.sigstore.json` bundle cover the exact release-asset bytes. NuGet.org repository-signs packages on ingestion, so its copy has a different digest and should be checked with `dotnet nuget verify --all`. See [Container Release Publishing](container-publish.md#release-signatures) for signer identities, verification commands, and trust boundaries.

## Dependency maintenance

OpenTelemetry ASP.NET Core and HTTP instrumentation packages are updated from 1.20.1 to 1.20.2. The template package ID, template identities, short name, supported options, public application surface, and `net10.0` target are unchanged.

## Updating

Install the template with `dotnet new install NetCoreApplicationTemplate::2.11.10`. Updating the template package affects future scaffolds; existing generated applications retain their current package references until updated separately.

See [issue #698](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/698), [issue #701](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/701), and the [changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md).
