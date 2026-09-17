# Governance

## Purpose

This document describes how **NetCoreApplicationTemplate** is governed: who maintains the project, how decisions are made, how contributions are triaged, and how releases are authorized.

NetCoreApplicationTemplate is a reusable, production-oriented ASP.NET Core application template. Governance decisions should protect secure defaults, predictable generated output, compatibility, documentation quality, and a reviewable release process.

## Roles and Responsibilities

### Core Maintainers

Core Maintainers have write access and final responsibility for project direction, quality, security, releases, and community health. Current operational ownership is recorded in [MAINTAINERS.md](MAINTAINERS.md).

| GitHub handle | Area of responsibility |
|:---|:---|
| [@cdcavell](https://github.com/cdcavell) | Architecture, template behavior, security, CI/CD, packaging, releases, documentation, and community stewardship |

Regular contributors who demonstrate sustained high-quality work, sound judgment, responsible handling of security and community concerns, and alignment with the project's purpose may be nominated as maintainers. Appointment requires a simple majority of current Core Maintainers with no active objection after a seven-day comment period.

While fewer than two active Core Maintainers are available, the current maintainer may handle routine changes, releases, security fixes, and governance updates after documenting the decision in the relevant issue, pull request, release record, or security advisory. This bootstrap exception does not remove the pull-request, CI, or release-evidence requirements documented by the repository.

### Triagers

Triagers may help label, organize, request information for, and close issues or pull requests when repository permissions allow. Triagers do not obtain merge or publishing authority through the role alone.

### Contributors

Anyone who opens an issue or discussion, submits a pull request, improves documentation, or otherwise participates constructively is a Contributor. Contributions are subject to the [Code of Conduct](CODE_OF_CONDUCT.md), [Community Standards](COMMUNITY_STANDARDS.md), and [Contributing](CONTRIBUTING.md) guidance.

### Emeritus Maintainers

Former maintainers who step back may be recognized as Emeritus Maintainers. They retain no voting, merge, administrative, or publishing authority unless separately granted a current role.

## Decision-Making Model

NetCoreApplicationTemplate uses consensus-seeking discussion with maintainer decision authority and a fallback vote when multiple Core Maintainers are active.

### Routine Decisions

Bug fixes, documentation corrections, dependency updates, tests, and other changes within an established design may proceed through a focused pull request after:

- Required CI and security checks pass.
- Review comments are resolved.
- A Core Maintainer confirms scope, compatibility, and release impact.
- Code Owner review is obtained when GitHub can obtain an independent approval.

For a self-authored pull request under the bootstrap solo-maintainer model, the maintainer reviews the final diff and evidence and follows the constrained merge path in [Repository Security Profile](docs/articles/repository-security-profile.md). The bootstrap path is not permission for routine direct pushes to `main`.

### Significant Decisions

Changes that materially alter public APIs, template options, generated behavior, package identity, supported frameworks, architecture, security posture, governance, or release strategy should begin with a GitHub issue or discussion. Unless security or operational urgency requires an expedited decision, allow at least five business days for public comment before adopting a significant proposal.

The decision record should explain the motivation, compatibility and migration impact, alternatives considered, validation plan, and ownership boundary between this reusable template and consuming applications.

### Disputed Decisions

When multiple Core Maintainers are active and consensus cannot be reached:

1. A Core Maintainer may request a formal vote with a written proposal.
2. Each active Core Maintainer casts one vote and provides rationale for a blocking vote.
3. A simple majority of votes cast decides the outcome.
4. A tie is resolved by the longest-serving active Core Maintainer.
5. The result is recorded in the related issue or discussion.

A Core Maintainer who believes a decision would create serious or irreversible harm may request a 48-hour hold for further review. Security containment may proceed more quickly when delay would increase risk.

## Issue and Pull Request Governance

Issues and pull requests follow the workflow in [CONTRIBUTING.md](CONTRIBUTING.md) and are triaged on a best-effort basis under [SUPPORT.md](SUPPORT.md).

Maintainers evaluate work for:

- Reproducibility and fit with the reusable template.
- Security, compatibility, packaging, generated-output, or release impact.
- Adequate tests, documentation, and validation evidence.
- Clear separation between template responsibilities and downstream application customization.
- A focused, reviewable scope.

The pull-request author is responsible for responding to review feedback and resolving conflicts. Maintainers may close duplicated, stale, abandoned, unreproducible, unsafe, or out-of-scope work with an explanation. Issue assignment does not create permanent ownership and may be released after prolonged inactivity.

## Release Governance

NetCoreApplicationTemplate follows Semantic Versioning and does not promise a fixed release calendar.

- Patch releases address compatible fixes, security issues, documentation-critical corrections, packaging defects, or release-validation improvements.
- Minor releases may add backward-compatible template options, generated behavior, or other compatible improvements.
- Major releases cover breaking generated behavior, supported-framework changes, package identity changes, or other incompatible contract changes.

Release preparation and evidence must follow [RELEASE.md](RELEASE.md). Release changes merge through protected `main`; publication tags point to the merged release commit; and protected environments control NuGet and container publication. A branch, tag, package, container, GitHub Release, and Zenodo record have distinct roles and must not be treated as interchangeable release evidence.

Security releases may use an expedited review window. Private reporting, disclosure, and vulnerability handling follow [SECURITY.md](SECURITY.md).

## AI-Assisted Development

AI-assisted tools may support coding, documentation, testing, and review. All contributions remain human-reviewed and human-owned. Maintainers are responsible for validating correctness, licensing, security, and alignment with project boundaries; an automated tool does not make project decisions or hold authorship, review, merge, or publishing authority.

## Changes to Governance

Minor clarifications may proceed through a normal pull request. Material changes to maintainer authority, decision rights, merge controls, publishing ownership, security governance, or project scope require the significant-decision process unless urgent security containment makes public advance discussion unsafe.

When maintainer ownership changes, update this file, [MAINTAINERS.md](MAINTAINERS.md), `.github/CODEOWNERS`, protected environments, repository permissions, and the repository security profile together.

## Related Documents

- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Community Standards](COMMUNITY_STANDARDS.md)
- [Contributing](CONTRIBUTING.md)
- [Maintainers](MAINTAINERS.md)
- [Support Policy](SUPPORT.md)
- [Security Policy](SECURITY.md)
- [Release Checklist and Runbook](RELEASE.md)

