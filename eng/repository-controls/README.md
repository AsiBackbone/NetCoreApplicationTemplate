# Repository Controls

This directory is the versioned desired state for the GitHub-hosted controls that protect NetCoreApplicationTemplate. Those controls live in GitHub, not in git, so this manifest records what they are supposed to be and a read-only audit compares it with the live repository.

These files are maintainer-only. They are not packed into the template package and are never scaffolded into generated applications.

## Files

| File | Purpose |
|:---|:---|
| `main-branch-ruleset.json` | The `Main branch security baseline` ruleset for `refs/heads/main`: deletion and force-push protection, linear history, pull-request requirements and allowed merge methods, the 12 required status checks, the CodeQL code-scanning thresholds, Copilot code review, and the bypass-actor list, which is deliberately empty because no actor may bypass the ruleset. It uses the GitHub ruleset payload shape without live-only fields such as `id`, `source`, or `_links`. |
| `repository-settings.json` | Security-analysis features, the merged-branch cleanup setting, and the publishing environment that must require reviewers. Each control is either `required` (drift fails the audit) or `preferred` (drift is a warning). |

## Audit

[`scripts/Test-RepositorySecurityControls.ps1`](../../scripts/Test-RepositorySecurityControls.ps1) reads the live repository with `gh api --method GET` and compares it with this manifest. It has no apply mode and sends no write requests; its self-test checks that by inspecting its own source.

Every control is reported as one of:

| Status | Meaning |
|:---|:---|
| `Match` | The live value matches the manifest. |
| `Drift` | The live value differs from the manifest. |
| `Missing` | The ruleset, rule, or environment does not exist. |
| `Unavailable` | GitHub did not return the value to the current token. The control is unverified, not compliant. |

A `Drift` or `Missing` result on a required control exits nonzero. `Unavailable` results exit nonzero only with `-FailOnUnavailable`.

Run the self-test after changing the script or manifest:

```powershell
./scripts/Test-RepositorySecurityControls.ps1 -SelfTest
```

Run the release-readiness audit with a repository-administrator `gh` login, so administrator-only controls such as secret scanning, push protection, branch cleanup, and ruleset bypass actors are verified rather than skipped:

```powershell
gh auth status
./scripts/Test-RepositorySecurityControls.ps1 -FailOnUnavailable -SummaryPath ./artifacts/repository-controls/repository-controls-audit.md
```

The [Repository Controls Audit](../../.github/workflows/repository-controls-audit.yml) workflow runs the same audit weekly, on demand, and on pull requests that change these files. It uses the read-only job token, so administrator-only controls appear as `Unavailable` there. The workflow writes the result table to the job summary and uploads it as the `repository-controls-audit` artifact.

## Changing a Control

1. Decide the change and update the [Repository Security Profile](../../docs/articles/repository-security-profile.md) in the same pull request as this manifest.
2. Change the live setting in GitHub yourself. The audit never applies changes.
3. Rerun the administrator audit and confirm every required control reports `Match`.

Renaming, adding, or removing a required CI job changes its status-check context. Update `main-branch-ruleset.json`, the live ruleset, and the profile's required-check list together.
