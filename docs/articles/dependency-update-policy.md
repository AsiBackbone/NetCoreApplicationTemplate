# Dependency Update Policy

This page defines how NetCoreApplicationTemplate (NCAT) balances routine dependency batching with timely security remediation for the dependency ecosystems configured in `.github/dependabot.yml`.

## Policy Summary

NCAT distinguishes **version updates** from **security updates**.

- Routine version updates may use a cooldown to reduce churn and allow newly released versions to stabilize before review.
- Dependabot security updates are enabled at the repository level and are not delayed by the `cooldown` option. GitHub documents cooldown as a version-update control.
- A security update still passes through the repository's normal pull-request validation, dependency review, vulnerability scanning, and maintainer review before merge.
- The repository does not intentionally add a fixed waiting period to a Dependabot security update.

This policy should be re-reviewed when GitHub changes Dependabot cooldown semantics, supported ecosystems, security-alert behavior, or GitHub Actions dependency handling.

## Ecosystem Decisions

| Ecosystem | Routine update posture | Security-sensitive posture |
|:---|:---|:---|
| NuGet | Weekly Friday check with a seven-day version-update cooldown and grouped dependency families. | Dependabot security updates bypass the version-update cooldown. Security PRs are reviewed with dependency review and vulnerability scanning before merge. |
| Docker | Weekly Friday check with a seven-day version-update cooldown. .NET base-image updates remain grouped. | Dependabot security updates bypass the version-update cooldown. Because the Dockerfile pins base images by digest, a patched image is adopted only after the digest changes and the resulting PR is merged. |
| GitHub Actions | Weekly Friday check with grouping. All Actions are explicitly excluded from cooldown. | GitHub can create Dependabot security updates for vulnerable Actions, but NCAT pins Actions by commit SHA and GitHub does not generate Dependabot alerts for SHA-pinned Actions. Removing cooldown from Actions avoids adding an extra delay to the weekly version check that compensates for this alerting limitation. |

## Why GitHub Actions Differ

Pinning Actions by commit SHA is an important supply-chain control because a workflow consumes a specific reviewed revision instead of a movable tag.

That protection has a tradeoff: GitHub documents Dependabot alerts for Actions as applying to semantic-version references rather than SHA references. Dependabot version updates can still advance supported SHA-pinned Actions, so NCAT keeps the weekly Actions update check but excludes all Actions from cooldown.

This is a deliberate compensating control, not a claim that SHA-pinned Actions receive the same advisory-driven update behavior as NuGet or Docker dependencies.

The existing workflow-security checks remain part of the compensating posture:

- `actionlint` validates workflow syntax and structure.
- `zizmor` analyzes GitHub Actions workflow security.
- Dependency review gates dependency changes before merge.
- CodeQL, OWASP Dependency-Check, and the repository's other required checks remain part of the broader release/security boundary.
- Actions remain pinned by immutable commit SHA and are reviewed when Dependabot proposes an update.

## Review and Merge Expectations

For routine dependency updates:

1. Allow Dependabot grouping and cooldown behavior to reduce unnecessary PR churn.
2. Review upstream release notes and compatibility impact.
3. Require the normal branch-protection checks before merge.

For a security-sensitive update:

1. Confirm the alert or advisory and the affected package, image, or Action.
2. Do not wait for the routine cooldown window.
3. Review the Dependabot PR or prepare an equivalent maintainer PR when automation cannot produce a usable update.
4. Run the normal dependency-review, build, template smoke-test, workflow-security, and vulnerability-scanning checks that apply to the change.
5. If a supported release is affected, assess whether the fix requires an expedited patch release rather than waiting for the next routine release.
6. Record any intentional deferral with the affected dependency, advisory or risk, rationale, compensating control, and explicit review trigger.

A security update is urgent input to the review process, not permission to bypass validation or publish an unverified artifact.

## Configuration and Behavior Verification

Changes to `.github/dependabot.yml` should be verified in two stages.

### Pull-request validation

Before merge:

1. Confirm the YAML parses successfully.
2. Confirm the three configured ecosystems remain `nuget`, `docker`, and `github-actions`.
3. Confirm NuGet and Docker retain `default-days: 7`.
4. Confirm GitHub Actions uses `cooldown.exclude: ["*"]` and does not inherit the routine seven-day delay.
5. Run the repository's normal workflow/documentation validation triggered by the configuration change.

### GitHub-hosted Dependabot verification

After merge, verify the behavior GitHub actually generates:

1. Open **Insights -> Dependency graph -> Dependabot** and review the update jobs for all three ecosystems.
2. Confirm NuGet and Docker routine version jobs report the intended seven-day cooldown behavior.
3. Confirm the GitHub Actions job does not defer eligible version updates because of cooldown.
4. When the next Dependabot security update is available, confirm it is created independently of the routine version-update cooldown.
5. If GitHub rejects or silently ignores a configuration key, treat that as a policy failure and correct the configuration before relying on the documented posture.

Generated Dependabot PR behavior is GitHub-hosted behavior; local YAML parsing alone cannot prove it.

## Coordination With Other Security Controls

This policy works together with, rather than replaces:

- [Repository Security Profile](repository-security-profile.md), including the requirement that Dependabot security updates remain enabled;
- [GitHub Workflow](github-workflow.md), including dependency and workflow review practices;
- [Container Release Publishing](container-publish.md), including pinned base-image and release-evidence handling;
- the repository-level [Security Policy](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/SECURITY.md);
- the stable-release checklist in [RELEASE.md](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/RELEASE.md).

Before a stable release, unresolved dependency or base-image security findings should be explicitly reviewed. A routine cooldown is not a justification to ship a known vulnerable dependency when a compatible remediation is available.

## Review Triggers

Revisit this policy when any of the following occurs:

- GitHub changes the documented scope of `cooldown`.
- Dependabot begins generating alerts for SHA-pinned GitHub Actions.
- NCAT stops pinning Actions by commit SHA.
- A security update is materially delayed by the current schedule or ecosystem behavior.
- A base-image advisory exposes a gap between digest pinning and update detection.
- Dependency-review, vulnerability-scanning, or release controls materially change.
