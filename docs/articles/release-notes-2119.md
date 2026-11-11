# NetCoreApplicationTemplate 2.11.9 Release Notes

2.11.9 is a patch release for the .NET 10 template. It updates the container images used by newly generated applications.

## Container security

The 2.11.8 NuGet package was published, but its signed container was blocked when Trivy reported CVE-2026-109518 in Ubuntu's APT packages. The 2.11.9 Dockerfile uses the refreshed Microsoft ASP.NET Core 10 runtime image. Trivy reported no HIGH or CRITICAL vulnerabilities in the refreshed Linux x64 base image during release preparation.

The Dockerfile also uses the .NET 10.0.404 SDK image. Dependabot groups the SDK and ASP.NET Core image updates and limits them to compatible .NET major versions.

## Updating

Install the template with `dotnet new install NetCoreApplicationTemplate::2.11.9`. For an existing generated application, update the pinned images in your application's Dockerfile, then rebuild and scan the application image before deployment. Updating the template package affects future scaffolds.

See [issue #688](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/688) and the [changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md).
