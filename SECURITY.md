# Security Policy

Thank you for taking the time to report security concerns responsibly.

NetCoreApplicationTemplate (NCAT) is a reusable, production-oriented ASP.NET Core application template providing startup and middleware organization, cookie authentication, authenticated-by-default routed endpoints, policy-based authorization, structured logging, centralized error handling, security headers, rate limiting, EF Core data-access patterns, health checks, and telemetry foundations.

NCAT is an application scaffold and architectural specimen. It is not a compliance certification, a security guarantee, a penetration-tested production application, or a substitute for application-specific security review.

## Supported Versions

Security fixes are applied to the current stable line unless otherwise noted in a release announcement.

| Version line | Supported | Notes |
|:---|:---:|:---|
| `2.x` | Yes | Current stable line under the `NetCoreApplicationTemplate` NuGet package identity. |
| `1.0.x` | Best effort | Legacy stable line under the previous `CDCavell.NetCoreApplicationTemplate` NuGet package identity. Upgrade to the current stable package when practical. |
| Pre-1.0 releases | Best effort | Preview releases are not guaranteed to receive backported security fixes. Upgrade to the current stable release when practical. |
| Older releases | Best effort | Support depends on severity, reproducibility, release impact, and maintainer availability. |
| `main` | Development | The development branch is not a supported release line. |

A report that affects the supported `2.x` line may still result in documentation, template, workflow, container, or release-process changes depending on where the actual risk lives.

## Reporting a Vulnerability or Sensitive Concern

Please do **not** place exploit details, secrets, proof-of-concept payloads, private keys, tokens, personal data, or sensitive operational information in a public Issue, pull request, Discussion, commit message, screenshot, or comment.

Preferred reporting path:

1. Open this repository's **Security** tab and select **Report a vulnerability** to use [GitHub private vulnerability reporting](https://github.com/AsiBackbone/NetCoreApplicationTemplate/security/advisories/new) when it is available.
2. Include a concise title and identify the affected repository area, version, branch, or commit when known.
3. Provide reproduction steps, expected behavior, actual behavior, and the practical security impact.
4. Use synthetic data and redact secrets or identifying information.
5. Allow reasonable time for review before public disclosure.

If private vulnerability reporting is unavailable, open a minimal public Issue stating only that you have a sensitive security report to share. Do not include technical details or sensitive material in that Issue.

For non-sensitive hardening suggestions, documentation corrections, or defense-in-depth improvements, a normal GitHub Issue or pull request is appropriate.

GitHub documents the private reporting mechanism and its review process under [repository security advisories](https://docs.github.com/en/code-security/concepts/vulnerability-reporting-and-management/repository-security-advisories).

### Additional detail useful for template reports

Because NCAT is a template rather than a deployed application, a report is easier to act on when it also identifies:

- whether the issue affects the repository source, the generated scaffold output, or both;
- which template options were used, such as `--authProvider` and `--dbProvider`;
- whether the issue affects default template behavior or only a specific consuming-application configuration;
- any relevant logs, configuration details, or proof-of-concept notes, with secrets removed.

## Expected Response Posture

<!-- VERIFY BEFORE COMMIT: the sibling repositories currently say "community-maintained" in this
     paragraph. That wording is inaccurate across the organization. The intent is to correct
     Learning and AsiBackbone to match the accurate wording below, not to reintroduce the
     inaccurate wording here. -->

This project is solo-maintained and does not promise a formal security-response SLA or fixed acknowledgment or remediation timelines.

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

The phrase **secure baseline** in this project refers to concrete controls: closed-by-default routed endpoints, explicit anonymous exceptions, startup validation, request protection, secure headers, rate limiting, and centralized error handling. Deployment-specific trust boundaries, provider registrations, credentials, network exposure, and business authorization remain the consuming application's responsibility.

## Repository-host security controls

<!-- VERIFY BEFORE COMMIT: this section assumes NCAT has adopted the sibling pattern of a
     version-controlled ruleset (eng/repository-controls/main-branch-ruleset.json), an audit
     script (scripts/Manage-RepositorySecurityControls.ps1), and a desired-state document.
     If those artifacts do not yet exist in this repository, either add them to match
     AsiBackbone and Learning, or replace this section with the previous manual-verification
     text and the existing docs/articles/repository-security-profile.md reference. Do not
     publish this section until the referenced paths resolve — lychee will fail the build. -->

Repository-host protections are treated as part of the project's supply-chain boundary rather than as an implicit GitHub administrator default. The canonical desired state is documented in [Repository Security Profile](docs/articles/repository-security-profile.md) and represented by `eng/repository-controls/main-branch-ruleset.json`.

The maintained posture requires secret scanning and secret-scanning push protection, an explicit ruleset for `main`, the existing release-blocking status checks, pull-request-only changes, linear history, and protection against branch deletion and force pushes.

Some controls remain repository- or organization-setting concerns rather than source-controlled workflow concerns. Their state cannot be inferred from committed YAML alone. Audit the live GitHub settings with:

```powershell
./scripts/Manage-RepositorySecurityControls.ps1
```

The script is read-only by default and exposes an explicit `-Apply` path for an authenticated repository administrator. Use `-WhatIf` before `-Apply`. Repository-host controls are not changed by a normal build, container, or package release workflow.

Verify secret scanning, push protection, Dependabot security updates, ruleset enforcement, and publishing-environment reviewers before each stable release and after material permission or publishing changes.

## Repository Secret-Scanning Controls

The `2.x` repository security profile requires GitHub secret scanning and secret-scanning push protection to remain enabled. Push protection is a preventive control: supported high-confidence secrets should be blocked before they reach repository history rather than relying only on post-commit alerts.

Provider-backed patterns supported by GitHub push protection should remain enabled. Generic, non-provider, and custom patterns are evaluated separately because broad patterns can create false positives. Add a custom pattern only when the repository has a concrete secret format that is not already covered, test the pattern against representative content, and enable push protection for that pattern when GitHub supports it and the false-positive rate is acceptable.

Validity checks should be enabled for supported provider patterns when the repository plan and GitHub feature set make them available. Treat validity as a remediation-priority signal, not permission to keep an exposed credential: a committed credential should still be revoked or rotated even when a validity check reports it inactive or cannot determine its state.

## Secrets Incident Response Playbook

If a credential, token, key, certificate, connection string, or other secret is suspected of being committed, disclosed, logged, or exposed through GitHub Actions:

1. Treat the secret as compromised immediately.
2. Revoke or rotate the secret at the issuing system before relying on repository cleanup.
3. Review recent GitHub Actions runs, repository events, package publishing events, container publishing events, and release activity for unexpected use.
4. Remove the exposed value from the current tree.
5. If the value exists in git history, rewrite history only after confirming the operational impact and coordinating protected branch updates.
6. Invalidate or replace any artifacts, packages, releases, or container images that may have been produced with the exposed secret.
7. Document whether the finding was a confirmed secret, a rotated secret, or a false positive.
8. Add or adjust preventive controls such as GitHub secret scanning, push protection, `.gitignore` rules, example configuration cleanup, or workflow permission tightening.
9. Avoid posting the exposed value in public issues, pull requests, commit messages, screenshots, or logs.

Repository cleanup does not invalidate a credential that has already been exposed. Rotation or revocation is the primary response.

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

NuGet package author signing remains deferred while the repository remains solo-maintained and official package artifacts are produced only through the maintainer-controlled release workflow. The dated decision record is [ADR-0005: Defer NuGet Package Signing](docs/adr/0005-defer-nuget-package-signing.md).

NuGet.org publication uses NuGet Trusted Publishing through GitHub Actions OIDC for the current `NetCoreApplicationTemplate` package line. Official package artifacts are produced only by the maintainer-controlled release workflow. External contributors do not publish NuGet packages directly and are not expected to sign generated `.nupkg` artifacts. If package signing is introduced later, release artifacts should be signed by a project-controlled certificate through the protected release workflow, not by individual contributors.

Until package author signing is formally adopted and documented, NCAT should not be described as providing author-signed release artifacts. NuGet.org adds its own repository signature to the package bytes it serves; that repository signature is distinct from a maintainer or author signature.

### Current trust model

Current release trust is established through:

- official NuGet package publication through Trusted Publishing;
- the public GitHub source repository and release tags;
- durable release-attached SPDX SBOMs for the package and the container image;
- package, SBOM, and container provenance attestations;
- the signed container image digest and published verification commands;
- release evidence and artifact SHA-256 mappings.

These mechanisms provide transparency and traceability while package author signing remains deferred. See [Container Release Publishing](docs/articles/container-publish.md#durable-release-assets) for the durable release assets and tested verification commands.

### External contributor trust

External contributor trust is handled separately from package signing. External contributions should enter through pull requests, required CI checks, Code Owner review when owned paths change, ruleset enforcement, dependency review, CodeQL and security scanning, and maintainer approval before merge.

Required signed commits are intentionally deferred under the current solo-maintainer profile. GitHub evaluates commits introduced by a pull request against signed-commit protection, so enabling the control without a repository-wide signing policy can block otherwise valid maintainer, automation, or contributor branches. Revisit commit-signature enforcement when an independent maintainer is added, a source-signing policy is established, or consumer or governance requirements justify the additional constraint. This decision does not weaken package or container publication controls: NuGet Trusted Publishing, protected environments, container keyless signing, and provenance remain separate release safeguards.

Revisit the package-signing decision when any of the following occur:

- before each stable NuGet package publication;
- before enabling fully automated package publication;
- before adding additional maintainers or package owners;
- before accepting external pull requests that affect workflows, package metadata, release automation, security policy, or template packaging;
- when consumers, organizations, or registries require signed packages;
- after a suspected credential, certificate, package, container, or release-workflow exposure;
- after repeated supply-chain, dependency, or publishing-risk findings;
- when package ownership moves to an organization or shared publishing model.

If package signing is enabled, document the signing certificate owner, certificate storage location, timestamping authority, certificate rotation process, revocation response, NuGet.org certificate registration expectations, workflow integration, and package verification steps before publishing signed artifacts.

## Security Scope

Areas especially relevant to this repository include:

- authentication and authorization configuration, including the fallback authorization policy and explicit anonymous exceptions;
- middleware ordering and request-pipeline behavior;
- security headers and Content Security Policy configuration;
- forwarded header and reverse-proxy handling;
- rate limiting;
- error handling and Problem Details responses;
- health endpoint exposure;
- data access and EF Core configuration;
- secret handling and configuration examples;
- generated scaffold output, including differences between template option combinations;
- GitHub Actions workflow behavior and permissions;
- template packaging, container publishing, and release workflow behavior.

### Where the issue lives

Security reports should distinguish between:

- issues in the template's default behavior or generated output;
- issues introduced by a consuming application's custom configuration or deployment environment;
- general dependency vulnerabilities already tracked by upstream packages or GitHub alerts.

### Examples generally in scope

- a routed endpoint that is reachable anonymously in the default scaffold without explicit anonymous metadata;
- a middleware-ordering defect that allows a request to bypass a documented protection;
- a generated scaffold that emits a real credential, a weak default secret, or an unsafe connection string;
- a workflow configuration that exposes a repository credential or grants unnecessarily dangerous write permissions in a way that creates a practical exploit path;
- documentation that materially misstates the template's authentication, authorization, or deployment security boundary;
- a container or package release-workflow issue that undermines the published provenance or evidence chain.

### Examples generally out of scope

- requests for legal, compliance, or certification guarantees;
- vulnerabilities caused solely by a consuming application's custom implementation, infrastructure, deployment configuration, cloud policy, database security, or key-management choices;
- reports that the `--authProvider none` opt-out produces publicly reachable endpoints; this is the documented behavior of an explicit architectural opt-out;
- reports that health endpoints are anonymous at the application layer; restricting their reachability is a documented deployment responsibility;
- vulnerabilities in `AsiBackbone/AsiBackbone` or `AsiBackbone/Learning` that are not present in NCAT; report those to the affected repository instead;
- issues that exist only in a third-party fork or materially modified copy of the template.

## Reports for Related Repositories

NCAT links to broader architectural material and to a complementary governance package family in other ASI Backbone organization repositories.

Security concerns in those projects should be reported to the repository that owns the affected code:

- [AsiBackbone security policy](https://github.com/AsiBackbone/AsiBackbone/security/policy)
- [ASI Backbone Learning security policy](https://github.com/AsiBackbone/Learning/security/policy)

If a concern exists both in NCAT and in a referenced repository, mention that relationship in the private report so maintainers can coordinate the correction.

## Related Documents

- [SUPPORT.md](SUPPORT.md)
- [MAINTAINERS.md](MAINTAINERS.md)
- [GOVERNANCE.md](GOVERNANCE.md)
- [CONTRIBUTING.md](CONTRIBUTING.md)
- [COMMUNITY_STANDARDS.md](COMMUNITY_STANDARDS.md)
- [RELEASE.md](RELEASE.md)
