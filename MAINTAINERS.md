# Maintainers

NetCoreApplicationTemplate currently operates under the bootstrap solo-maintainer model defined in [GOVERNANCE.md](GOVERNANCE.md). This file records operational ownership; `GOVERNANCE.md` remains authoritative for project roles and decision-making.

## Current Maintainer

| Maintainer | Role |
|:---|:---|
| [@cdcavell](https://github.com/cdcavell) | Repository owner, release owner, package publishing owner, documentation owner |

## Maintainer Responsibilities

The maintainer is responsible for:

- Reviewing and merging pull requests.
- Maintaining branch protection expectations for `main`.
- Reviewing workflow, security, release, and template packaging changes.
- Approving package publication through protected GitHub environments.
- Managing NuGet package identity and publish access.
- Managing GitHub release and Zenodo archival sequencing.
- Reviewing vulnerability reports and coordinating security fixes.
- Keeping release, support, and contribution documentation current.
- Maintaining CODEOWNERS coverage and required Code Owner review for protected branches.
- Reviewing stale approval behavior when branch protection settings change.

## Release Cadence

This project does not promise a fixed release calendar.

Expected release behavior:

- Patch releases may be created for security fixes, packaging corrections, documentation-critical fixes, or small compatible improvements.
- Minor releases may be created for compatible template improvements, new supported options, or expanded documentation.
- Major releases may be created for breaking template behavior, supported framework changes, major packaging changes, package identity changes, or significant governance changes.
- `v2.0.0` represents the current stable package identity baseline under the `NetCoreApplicationTemplate` NuGet package ID.
- `v1.0.x` remains a legacy stable line under the previous `CDCavell.NetCoreApplicationTemplate` package identity.

Release timing depends on issue readiness, CI health, package validation, documentation readiness, and maintainer availability.

## Publishing Ownership

Official release artifacts are published only by the maintainer-controlled release workflow.

Publishing ownership includes:

- NuGet package publication for `NetCoreApplicationTemplate`.
- Legacy NuGet package guidance for `CDCavell.NetCoreApplicationTemplate` when needed.
- GitHub Releases and release notes.
- Zenodo archival metadata and DOI-bearing GitHub releases.
- GitHub Container Registry publication when container releases are used.
- Release checklist approval documented in [RELEASE.md](RELEASE.md).

External contributors are not expected to sign or publish package artifacts. If NuGet package signing is introduced later, signing should use a project-controlled signing certificate and protected release workflow.

## Branch Protection Expectations

The `main` branch is the stable integration branch and should be protected.

Expected `main` branch controls include:

- Changes flow through pull requests instead of routine direct pushes.
- Required status checks pass before merge.
- Pull requests targeting `main` require Code Owner review when owned paths are changed and GitHub can obtain an independent Code Owner approval.
- Stale pull request approvals are dismissed when new reviewable commits are pushed.
- General required approvals remain at zero while the repository has only one maintainer.
- Approval of the most recent reviewable push remains disabled while no second authorized reviewer exists.
- Required signed commits remain deferred until a repository-wide signing policy is practical.
- Force pushes and branch deletion remain disabled for `main`.
- Linear history or squash/rebase merge strategy is preserved according to repository settings.
- Administrator bypass is retained only for the solo-maintainer self-authored PR path and documented emergencies; it is not a routine direct-push path.
- Workflow, release, security, package, and governance changes receive deliberate maintainer review.

The authoritative rationale and emergency procedure are documented in [Repository Security Profile](docs/articles/repository-security-profile.md).

Short-lived `release/*` branches do not carry independent publishing authority. They are created from `main`, validated, merged back through protected `main`, and only then may the merged `main` commit be tagged for publication. Protect any release branch equivalently to `main` if it becomes long-lived or begins accepting independent changes.

## Adding Maintainers

Maintainer appointments follow [GOVERNANCE.md](GOVERNANCE.md). Before expanding maintainership, review:

- Repository permissions and the documented v2.x security profile.
- Branch protection rules, including enabling independent approval requirements where appropriate.
- Whether approval of the most recent reviewable push should now be required.
- Whether administrator bypass can be removed or narrowed.
- Whether required signed commits should now be enabled.
- Environment protection rules.
- NuGet and GitHub Packages publishing permissions.
- CODEOWNERS coverage.
- Security reporting and release ownership expectations.

Update this file when maintainer ownership changes.
