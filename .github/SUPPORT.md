# Support Policy

This project provides a reusable ASP.NET Core application template. Support is focused on the template source, generated scaffold behavior, documentation, packaging, and release artifacts maintained in this repository.

## Support Channels

Use [GitHub Discussions](https://github.com/AsiBackbone/NetCoreApplicationTemplate/discussions) for:

- Setup and usage questions.
- Design, extension, or application-specific guidance that is not a template defect.
- Community feedback and open-ended ideas that are not yet focused feature requests.

Use [GitHub Issues](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues) for:

- Reproducible bugs in the template source or generated scaffold output.
- Documentation gaps or incorrect examples.
- Template packaging, installation, or `dotnet new` scaffold issues.
- Security-adjacent behavior that is not a private vulnerability report.
- Focused feature requests that improve the reusable baseline.

Use the private vulnerability reporting process described in [SECURITY.md](SECURITY.md) for suspected vulnerabilities.

## Support Expectations

Support is provided on a best-effort basis by the repository maintainer for the current stable release line.

Users can expect:

- Public issue triage when enough information is provided.
- Security reports to receive higher priority than general feature requests.
- Reproducible template defects to be prioritized over application-specific customization requests.
- Documentation fixes to be accepted when they clarify supported usage.
- Patch releases when a fix is appropriate for the current stable line.

Users should not expect:

- Guaranteed service-level agreements or response times.
- Private consulting, production incident response, or environment-specific debugging.
- Backports to every historical release line.
- Support for heavily modified downstream applications unless the issue reproduces from the template baseline.
- Support for unsupported .NET SDK versions or package versions outside the documented release line.

## Version Support Lifecycle

| Version line | Support expectation |
|:---|:---|
| `2.0.x` | Current stable line. Supported for reproducible defects, security fixes, package identity corrections, and documentation fixes. |
| `1.0.x` | Legacy stable line under the previous NuGet package identity. Best effort unless a release note states otherwise. |
| Pre-1.0 releases | Best effort only. Consumers should upgrade to the current stable release when practical. |
| Older stable releases after a newer minor or major release | Best effort unless a release note states otherwise. |
| Unreleased `main` branch | Development line only. Behavior may change before the next release. |

## Issue Triage

Issues are generally reviewed for:

1. Reproducibility.
2. Security or release impact.
3. Whether the behavior belongs in the reusable template.
4. Whether the report includes enough detail to act on.
5. Whether the fix can be safely validated by CI, tests, or documentation review.

Maintainers may close issues that are stale, unreproducible, out of scope, duplicated, or specific to a downstream application customization. Decision authority and contribution ownership are defined in [GOVERNANCE.md](GOVERNANCE.md).

## Pull Request Support

Pull requests should follow [CONTRIBUTING.md](CONTRIBUTING.md). Maintainer review is required before merge.

Large or broad pull requests may be redirected into smaller issues. Pull requests that change release, security, workflow, template packaging, or governance behavior may require additional review even when CI passes.
