# GitHub Workflow

This project uses Git for local source control with a remote repository hosted on GitHub.

## Branch Naming

Recommended branch naming:

```text
main
feature/issue-<issue-number>
fix/issue-<issue-number>
docs/issue-<issue-number>
refactor/issue-<issue-number>
test/issue-<issue-number>
chore/issue-<issue-number>
```

Example:

```text
feature/issue-42
```

## Commit Style

Recommended commit style:

```text
Add initial repository attributes #<issue-number>
Add application README scaffold #<issue-number>
Implement security header middleware #<issue-number>
Configure Serilog request logging #<issue-number>
Add EF Core SQLite provider #<issue-number>
```

Automated dependency update commits should use the Dependabot-generated format with the `chore(deps)` prefix.

## Pull Request Expectations

Pull requests should include:

- A short summary of the change.
- A validation or testing section.
- A closing issue reference when applicable, such as `Closes #42`.
- Notes about behavior changes, migration steps, or deployment impact when relevant.

Prefer small, focused pull requests. Documentation-only, dependency-only, and runtime behavior changes should usually be kept separate.

## Branch Protection

The `main` branch is treated as the stable integration branch. Changes should be made through pull requests rather than direct pushes.

Pull requests targeting `main` require Code Owner review when files match `.github/CODEOWNERS`.

GitHub branch protection is configured to dismiss stale pull request approvals when new reviewable commits are pushed. This ensures pull requests are re-evaluated after changes.

General required approval counts are not currently enabled under the solo-maintainer model. This may be enabled later when additional maintainers are added.

Before merging, review that:

- The branch is current enough to merge cleanly.
- Required validation checks have passed.
- Required Code Owner review has been approved when owned files are changed.
- Any stale approvals caused by new commits have been re-approved.
- The pull request scope matches the issue or stated goal.
- Documentation has been updated when behavior or workflow expectations change.

After changing workflow triggers, review branch protection required checks so old push-scoped duplicate check names are not still required.

## CI Validation

The CI workflow validates pull requests, `main` branch updates, release tags, and manual workflow runs.

Current validation includes:

- Dependency restore.
- Release build.
- Formatting verification.
- Test execution.
- Coverage report generation.
- Initial coverage threshold enforcement.
- CodeQL analysis.
- Template package smoke testing on Linux, Windows, and macOS.
- Scaffolded Docker support file verification.

The CI trigger scope is intentionally limited to:

- Pull requests targeting `main`.
- Pushes to `main`.
- Release-style tags matching `v*.*.*`.
- Manual `workflow_dispatch` runs.

Feature-branch pushes are not CI triggers by default. Feature branches are validated through pull requests so the same branch state does not produce duplicate push and pull-request smoke-test checks.

Dependency update pull requests should be reviewed with the same CI expectations as manually authored pull requests.

## Documentation Publishing

Documentation is built with DocFX and published to GitHub Pages from `main`.

Documentation updates should be validated by checking:

- Navigation entries are present.
- New markdown files are included in `docs/docfx.json` when needed.
- Resource files such as images or examples are included as DocFX resources when needed.
- Links are relative and work in the published site.

## Container Publishing

The Publish Container workflow runs on tag pushes matching:

```text
v*.*.*
```

The workflow builds the Docker image, scans it with Trivy, uploads SARIF results, generates an SPDX SBOM, publishes the image to GitHub Container Registry, signs the pushed digest with cosign keyless signing, and generates build provenance attestation metadata.

The published image is:

```text
ghcr.io/AsiBackbone/netcoreapplicationtemplate
```

Stable tags publish the full version tag, the major tag, and `latest`. Prerelease tags publish only the full version tag.

The publish job uses the `container-publish` GitHub environment. Configure that environment with required reviewers before the first production publish so the first GHCR publication has a manual approval gate.

See [Container Release Publishing](container-publish.md) for details.

## Dependency Update Automation

The repository uses Dependabot to monitor supported dependency ecosystems.

Dependabot is configured in `.github/dependabot.yml` for:

- NuGet packages used by project files.
- GitHub Actions used by workflow files.

Dependabot runs weekly on Monday morning in the `America/Chicago` timezone.

## Dependency Grouping

NuGet updates are grouped by related package families where practical:

- Microsoft ASP.NET Core packages.
- Microsoft Entity Framework Core packages.
- Serilog packages.
- OpenTelemetry packages.
- Test dependencies.
- External authentication dependencies.

GitHub Actions updates are grouped together so workflow action updates can be reviewed as a focused maintenance pull request.

Grouping helps reduce pull request noise while keeping related packages aligned.

## Dependency Update Review Expectations

When reviewing dependency update pull requests:

- Confirm CI passes before merging.
- Read release notes for major version updates.
- Review security updates promptly.
- Be cautious with authentication, data access, middleware, and telemetry dependencies because they can affect runtime behavior.
- Prefer merging grouped patch and minor updates after validation.
- Consider separating or manually testing major updates that affect startup, authentication, EF Core, logging, or GitHub Actions behavior.
- Watch for generated changes that modify lock files, project files, workflow files, or transitive dependency expectations.

Dependency updates should not be treated as automatic merges. They are maintenance pull requests that still require review.

## Issue Tracking

Issues should describe the intended change clearly enough that a future maintainer can understand why the work was done.

Pull requests should reference their issue with a closing keyword when the work completes the issue:

```text
Closes #42
```

For exploratory or partial work, use a non-closing reference instead:

```text
Related to #42
```

## Required Secret

Some workflow automation may require a classic GitHub personal access token stored as:

```text
PROJECT_TOKEN
```

Required classic PAT scopes:

- `project`
- `repo` if the repository is private
- `public_repo` may be sufficient if the repository is public

A fine-grained PAT may not work for user-owned GitHub Projects.
