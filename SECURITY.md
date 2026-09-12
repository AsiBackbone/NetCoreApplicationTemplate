# Security Policy

## Supported Versions

Security fixes are applied to the current stable line unless otherwise noted in a release announcement.

| Version line | Supported | Notes |
|:---|:---:|:---|
| `2.x` | Yes | Current stable line under the `NetCoreApplicationTemplate` NuGet package identity. |
| `1.0.x` | Best effort | Legacy stable line under the previous `CDCavell.NetCoreApplicationTemplate` NuGet package identity. Upgrade to the current stable package when practical. |
| Pre-1.0 releases | Best effort | Preview releases are not guaranteed to receive backported security fixes. Upgrade to the current stable release when practical. |
| Older releases | Best effort | Support depends on severity, reproducibility, release impact, and maintainer availability. |
| `main` | Development | The development branch is not a supported release line. |

## Reporting a Vulnerability or Sensitive Concern

Please do **not** place exploit details, secrets, proof-of-concept payloads, private keys, tokens, personal data, or sensitive operational information in a public Issue, pull request, Discussion, commit message, screenshot, or comment.

Preferred reporting path:

1. Open this repository's **Security** tab and select **Report a vulnerability** to use GitHub private vulnerability reporting when it is available.
2. Include a concise title and identify the affected repository area, version, branch, or commit when known.
3. Provide reproduction steps, expected behavior, actual behavior, and the practical security impact.
4. Use synthetic data and redact secrets or identifying information.
5. Allow reasonable time for review before public disclosure.

If private vulnerability reporting is unavailable, open a minimal public Issue stating only that you have a sensitive security report to share. Do not include technical details or sensitive material in that Issue.

For non-sensitive hardening suggestions, documentation corrections, or defense-in-depth improvements, a normal GitHub Issue or pull request is appropriate.

## Expected Response Posture

This is a community-maintained open-source project and does not promise a formal security-response SLA or fixed acknowledgment or remediation timelines.

The expected best-effort process is:

1. A maintainer reviews the report and determines whether it is a vulnerability, documentation issue, sample or template issue, workflow or dependency concern, hardening opportunity, duplicate, or out-of-scope report.
2. The maintainer may request clarification, affected-version or commit information, sanitized logs, or a reduced reproduction.
3. Confirmed concerns are addressed through code, documentation, dependency, workflow, repository-configuration, release, or advisory changes appropriate to the risk.
4. Public communication distinguishes confirmed behavior from suspected risk and avoids overstating security, compliance, legal, or operational guarantees.

Please avoid repeated public disclosure while a sensitive report is being reviewed.

## Sensitive Data Guidance for Reports

When reporting a concern:

- redact passwords, secrets, tokens, private keys, certificates, connection strings, user identifiers, personal information, customer data, and regulated data;
- use synthetic examples whenever possible;
- share only the minimum information required to reproduce or understand the concern;
- clearly identify any material that remains sensitive.

## Safe Public Language Expectations

Public communication should distinguish implemented controls from intended architecture, repository behavior from downstream or consuming-system behavior, and verified evidence from assumptions.

Do not describe a repository, package, template, sample, workflow, or generated application as vulnerability-free, automatically compliant, legally sufficient, tamper-proof, or production-ready solely because a documented control exists or automated checks pass.

## Template Disclosure Context

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

## Repository Secret-Scanning Controls

The v2.x repository security profile requires GitHub secret scanning and secret-scanning push protection to remain enabled. Push protection is a preventive control: supported high-confidence secrets should be blocked before they reach repository history rather than relying only on post-commit alerts.

Provider-backed patterns supported by GitHub push protection should remain enabled. Generic, non-provider, and custom patterns are evaluated separately because broad patterns can create false positives. Add a custom pattern only when the repository has a concrete secret format that is not already covered, test the pattern against representative content, and enable push protection for that pattern when GitHub supports it and the false-positive rate is acceptable.

Validity checks should be enabled for supported provider patterns when the repository plan and GitHub feature set make them available. Treat validity as a remediation-priority signal, not permission to keep an exposed credential: a committed credential should still be revoked or rotated even when a validity check reports it inactive or cannot determine its state.

Repository settings are not version-controlled. Verify secret scanning, push protection, Dependabot security updates, branch protection, and publishing-environment reviewers against the [Repository Security Profile](docs/articles/repository-security-profile.md) before each stable release and after material permission or publishing changes.

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

NuGet package author signing remains deferred under [ADR-0005](docs/adr/0005-defer-nuget-package-signing.md). The decision is owned by the repository and package publishing owner and must be reviewed no later than 2027-03-31 or before the first 3.0.0 release candidate, whichever occurs first.

NuGet.org publication uses NuGet Trusted Publishing through GitHub Actions OIDC for the current `NetCoreApplicationTemplate` package line. Trusted Publishing authenticates the publishing workflow; it does not sign the `.nupkg`. Official package artifacts are produced only by the maintainer-controlled release workflow. External contributors do not publish NuGet packages directly and are not expected to sign generated `.nupkg` artifacts. If package signing is introduced later, release artifacts should be signed by a project-controlled certificate through the protected release workflow, not by individual contributors.

Cosign keyless signing covers only the published OCI image digest. Package provenance attestations, SPDX SBOMs, Source Link metadata, release tags, and SHA-256 hashes are independently useful evidence, but none is a NuGet author signature. Tagged releases durably attach that evidence and enumerate it in `release-evidence-manifest.json`; GitHub Actions artifacts are temporary staging and diagnostic copies only.

External contributor trust is handled separately from package signing. External contributions should enter through pull requests, required CI checks, Code Owner review when owned paths change, branch protection, dependency review, CodeQL/security scanning, and maintainer approval before merge.

Required signed commits are intentionally deferred under the current solo-maintainer profile. GitHub evaluates commits introduced by a pull request against signed-commit protection, so enabling the control without a repository-wide signing policy can block otherwise valid maintainer, automation, or contributor branches. Revisit commit-signature enforcement when an independent maintainer is added, a source-signing policy is established, or consumer/governance requirements justify the additional constraint. This decision does not weaken package or container publication controls: NuGet Trusted Publishing, protected environments, container keyless signing, and provenance remain separate release safeguards.

Revisit ADR-0005 by its mandatory deadline and sooner when any of the following conditions occur:

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

- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- [GOVERNANCE.md](GOVERNANCE.md)
- [SUPPORT.md](SUPPORT.md)
- [MAINTAINERS.md](MAINTAINERS.md)
- [CONTRIBUTING.md](CONTRIBUTING.md)
- [RELEASE.md](RELEASE.md)
