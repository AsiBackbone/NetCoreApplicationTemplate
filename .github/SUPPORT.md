# Support Policy

This project provides a reusable ASP.NET Core application template. Support is focused on the template source, generated scaffold behavior, documentation, packaging, and release artifacts maintained in this repository.

## Maintenance Posture

The `2.x` line reached feature completion at release `2.10.0`.

**In scope for future releases**

- Security fixes, including CVE remediation in dependencies and pinned container images.
- Dependency servicing and lock-file refreshes.
- Reproducible defects in the template source or generated scaffold output.
- Documentation corrections, clarifications, and link maintenance.
- Build, test, and release-workflow maintenance required to keep the above shippable.

**Out of scope**

- New template options or configuration surfaces.
- New runtime capabilities or application-layer subsystems.
- Changes to generated scaffold structure or default runtime behavior, except where required by a security fix.

Security fixes that change default behavior remain possible. Where one is required, it is documented in the changelog under both `Security` and `Compatibility`, as with the forwarded-header trust change in `2.8.0`.

Generated projects are not modified by later releases. Applications scaffolded from any `2.x` version continue to build and run independently of the template's release cadence.

## Support Channels

Use [GitHub Discussions](https://github.com/AsiBackbone/NetCoreApplicationTemplate/discussions) for:

- Setup and usage questions.
- Design, extension, or application-specific guidance that is not a template defect.
- Community feedback and ideas. Capability ideas are welcome here as a record for any future line, but are not planned work for `2.x`.

Use [GitHub Issues](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues) for:

- Reproducible bugs in the template source or generated scaffold output.
- Documentation gaps or incorrect examples.
- Template packaging, installation, or `dotnet new` scaffold issues.
- Security-adjacent behavior that is not a private vulnerability report.
- Feature requests, which are acknowledged and closed with reference to the maintenance posture above. Open a discussion instead if the idea is worth recording for a future line.

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
- New features, options, or configuration surfaces in the `2.x` line.
- 
## Version Support Lifecycle

| Version line | Support expectation |
|:---|:---|
| `2.x` | Current stable line, feature-complete as of `2.10.0`. Supported for security fixes, dependency servicing, reproducible defects, and documentation fixes. |
| `1.0.x` | Legacy stable line under the previous NuGet package identity. Best effort unless a release note states otherwise. |
| Pre-1.0 releases | Best effort only. Consumers should upgrade to the current stable release when practical. |
| Older stable releases after a newer minor or major release | Best effort unless a release note states otherwise. |
| Unreleased `main` branch | Development line only. Behavior may change before the next release. |

## Issue Triage

Issues are generally reviewed for:

1. Reproducibility.
2. Security or release impact.
3. Whether the change is in scope under the maintenance posture above.
4. Whether the report includes enough detail to act on.
5. Whether the fix can be safely validated by CI, tests, or documentation review.

Maintainers may close issues that are stale, unreproducible, out of scope, duplicated, or specific to a downstream application customization. Decision authority and contribution ownership are defined in [GOVERNANCE.md](GOVERNANCE.md).

## Pull Request Support

Pull requests should follow [CONTRIBUTING.md](CONTRIBUTING.md). Maintainer review is required before merge.

Large or broad pull requests may be redirected into smaller issues. Pull requests that change release, security, workflow, template packaging, or governance behavior may require additional review even when CI passes.
