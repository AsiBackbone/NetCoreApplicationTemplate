# Security Policy

## Supported Versions

Security fixes are applied to the current stable line unless otherwise noted in a release announcement.

| Version line | Supported | Notes |
|:---|:---:|:---|
| `2.0.x` | Yes | Current stable line under the `NetCoreApplicationTemplate` NuGet package identity. |
| `1.0.x` | Best effort | Legacy stable line under the previous `CDCavell.NetCoreApplicationTemplate` NuGet package identity. Upgrade to the current stable package when practical. |
| Pre-1.0 releases | Best effort | Preview releases are not guaranteed to receive backported security fixes. Upgrade to the current stable release when practical. |
| Older releases | Best effort | Support depends on severity, reproducibility, release impact, and maintainer availability. |
| `main` | Development | The development branch is not a supported release line. |

## Reporting a Vulnerability

Please report suspected security issues privately instead of opening a public GitHub issue.

Use [GitHub private vulnerability reporting](https://github.com/AsiBackbone/NetCoreApplicationTemplate/security/advisories/new) to submit a confidential report directly to the repository maintainer. If that form is unavailable, use the repository owner's GitHub profile contact options and do not include sensitive details in a public issue or discussion.

When reporting a vulnerability, include:

- A clear description of the issue.
- Steps to reproduce the behavior.
- The affected version, branch, or commit if known.
- Any relevant logs, screenshots, configuration details, or proof-of-concept notes.
- Whether the issue affects default application behavior or only a specific consuming application configuration.

## Disclosure Expectations

Please allow reasonable time for review and remediation before publicly discussing a suspected vulnerability.

This project is a reusable application template. Security reports should distinguish between:

- Issues in the template's default behavior.
- Issues introduced by a consuming application's custom configuration or deployment environment.
- General dependency vulnerabilities already tracked by upstream packages or GitHub alerts.

## Security Scope

Areas especially relevant to this application include:

- Authentication and authorization configuration.
- Security headers.
- Forwarded header handling.
- Rate limiting.
- Error handling and Problem Details responses.
- Data access configuration.
- Secret handling and configuration examples.
- GitHub Actions workflow behavior.
- Template packaging and release workflow behavior.

Do not include production secrets, private keys, tokens, passwords, or sensitive personal data in a report.

## Secrets Incident Response Playbook

If a credential, token, key, certificate, connection string, or other secret is suspected of being committed, disclosed, logged, or exposed through GitHub Actions:

1. Treat the secret as compromised immediately.
2. Revoke or rotate the secret at the issuing system before relying on repository cleanup.
3. Review recent GitHub Actions runs, repository events, package publishing events, and release activity for unexpected use.
4. Remove the exposed value from the current tree.
5. If the value exists in git history, rewrite history only after confirming the operational impact and coordinating protected branch updates.
6. Invalidate or replace any artifacts, packages, releases, or container images that may have been produced with the exposed secret.
7. Document whether the finding was a confirmed secret, a rotated secret, or a false positive.
8. Add or adjust preventive controls such as GitHub secret scanning, push protection, `.gitignore` rules, example configuration cleanup, or workflow permission tightening.
9. Avoid posting the exposed value in public issues, pull requests, commit messages, screenshots, or logs.

Secrets scan reports should be stored outside tracked source control, preferably under ignored local paths such as `artifacts/security/`.

## Repository Publish Permissions

Repository and environment permissions must be scoped to the narrowest workflow that requires them.

| Permission or credential | Intended use | Scope expectation |
|:---|:---|:---|
| `GITHUB_TOKEN` | Built-in workflow token | Use explicit workflow or job-level permissions. Default to `contents: read` unless a job needs more. |
| GitHub Actions OIDC token | NuGet Trusted Publishing login for NuGet.org package publication | Grant `id-token: write` only to the package publication workflow/job that needs NuGet Trusted Publishing. |
| Package publishing permissions | Package release workflow | Limit to release or manual publish workflows. Do not grant publish permissions to normal CI validation jobs. |
| Release permissions | GitHub release automation | Grant `contents: write` only to the workflow/job that creates or updates releases. |

Plaintext credentials are not allowed in workflow files, repository files, examples, documentation, screenshots, or committed logs.

## Package Signing and External Contributor Controls

NuGet package author signing remains deferred while the repository remains solo-maintained and official package artifacts are produced only through the maintainer-controlled release workflow.

NuGet.org publication uses NuGet Trusted Publishing through GitHub Actions OIDC for the current `NetCoreApplicationTemplate` package line. Official package artifacts are produced only by the maintainer-controlled release workflow. External contributors do not publish NuGet packages directly and are not expected to sign generated `.nupkg` artifacts. If package signing is introduced later, release artifacts should be signed by a project-controlled certificate through the protected release workflow, not by individual contributors.

External contributor trust is handled separately from package signing. External contributions should enter through pull requests, required CI checks, Code Owner review when owned paths change, branch protection, dependency review, CodeQL/security scanning, and maintainer approval before merge. Signed commits may be required later if the repository moves beyond the solo-maintainer profile.

Revisit the package-signing decision when any of the following conditions occur:

- Before each stable NuGet package publication.
- Before enabling fully automated package publication.
- Before adding additional maintainers or package owners.
- Before accepting external pull requests that affect workflows, package metadata, release automation, security policy, or template packaging.
- When consumers, organizations, or registries require signed packages.
- After a suspected credential, certificate, package, or release-workflow exposure.
- After repeated supply-chain, dependency, or publishing-risk findings.
- When package ownership moves to an organization or shared publishing model.

If package signing is enabled, document the signing certificate owner, certificate storage location, timestamping authority, certificate rotation process, revocation response, NuGet.org certificate registration expectations, workflow integration, and package verification steps before publishing signed artifacts.

## Related Documents

- [SUPPORT.md](SUPPORT.md)
- [MAINTAINERS.md](MAINTAINERS.md)
- [CONTRIBUTING.md](CONTRIBUTING.md)
- [RELEASE.md](RELEASE.md)
