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

The `main` branch is the stable integration and publishing source branch. The authoritative v2.x control decisions are documented in [Repository Security Profile](repository-security-profile.md).

Current solo-maintainer behavior is intentionally asymmetric: external or automation-authored changes to owned paths require maintainer Code Owner review, while maintainer-authored pull requests cannot obtain independent self-approval. General required approvals therefore remain at zero, approval of the most recent reviewable push remains disabled, and required signed commits remain deferred. Administrator bypass is retained only for the documented self-authored PR path and emergencies; routine direct pushes to `main` are not part of the workflow.

Before merging, review that:

- The branch is current enough to merge cleanly.
- Required validation checks have passed.
- Required Code Owner review has been approved when an independent Code Owner approval is available.
- Any stale approvals caused by new reviewable commits have been re-approved.
- The pull request scope matches the issue or stated goal.
- Documentation has been updated when behavior or workflow expectations change.

After changing workflow triggers, review branch protection required checks so old push-scoped duplicate check names are not still required.

### Release Branches and Tags

Short-lived `release/*` branches are preparation branches, not an independent protection or publishing boundary. Create them from the current `main`, validate the release changes, and merge them back through a pull request to protected `main`.

Do not create a production `v*.*.*` tag from an unmerged release branch. Package and container publication tags should resolve to the release commit already merged into `main`. If a release branch becomes long-lived or starts accepting changes that do not immediately flow through `main`, protect it equivalently to `main`.

`main` is the only permanently retained branch. The repository automatically deletes eligible head branches after merge, including `release/*` branches. Tags, GitHub Releases, release artifacts, and the merged `main` commit preserve release evidence. Before manually deleting any legacy release branch, verify that its changes reached `main` and that its immutable version tag and published GitHub Release exist. Retain an active non-`main` branch only while an open pull request or explicitly documented follow-up still needs it.

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

## Software Composition Analysis

OWASP Dependency-Check runs in its own workflow, separately from CI. It scans the restored NuGet dependency graph and uploads SARIF results to GitHub code scanning. It runs weekly on a schedule, on `workflow_dispatch`, and on pushes to `main` that touch project or lock files. It does not run on pull requests.

**It reports; it does not gate.** The scan is not invoked with `--failOnCVSS`, so a finding surfaces as a code-scanning alert rather than failing a build. That is deliberate given the trigger scope: a gate on a workflow that runs after merge would fail `main` rather than the change that introduced the finding, and every cross-ecosystem false positive would hold `main` red until a suppression was added.

Findings are reviewed through code scanning, and the release checklist in `RELEASE.md` asks for explicit confirmation that dependency and audit scanning has no unresolved release-blocking findings before a stable tag. Treat that confirmation as the gate, not the workflow's exit code.

Reviewed false positives are recorded in `dependency-check-suppressions.xml`. Each entry is scoped to an exact package and version so it expires naturally when that version is no longer referenced; prune entries whose versions are no longer in `Directory.Packages.props`.

## Documentation Publishing

Documentation is built with DocFX and published to GitHub Pages from `main`.

Documentation updates should be validated by checking:

- Navigation entries are present.
- New markdown files are included in `docs/docfx.json` when needed.
- Resource files such as images or examples are included as DocFX resources when needed.
- Links are relative and work in the published site.

## NuGet Package Publishing

The Publish Template Package workflow publishes release tags and can also be run manually for pack-only validation. NuGet.org publication uses NuGet Trusted Publishing through GitHub Actions OIDC rather than a long-lived NuGet API key. For tags, the workflow also generates and attests evidence for the exact `.nupkg`, then attaches its SPDX SBOM and SHA-256 package manifest to the GitHub Release.

The publish job uses the `template-package-publish` GitHub environment. Keep deliberate maintainer approval on that environment, and keep `id-token: write` scoped only to the job that performs Trusted Publishing login. Changes to package publishing, environment names, OIDC identity, package ownership, or registry targets require security/release review before the next stable tag.

## Container Publishing

The Publish Container workflow runs on tag pushes matching:

```text
v*.*.*
```

The workflow builds the Docker image, scans it with Trivy, uploads SARIF results, generates a container SPDX SBOM, publishes the image to GitHub Container Registry, signs the pushed digest with Cosign keyless signing, and generates build provenance attestation metadata. It then waits for the package workflow's evidence, creates the unified release evidence manifest, uploads the complete durable asset set, and verifies that the public release contains every required asset.

The published image is:

```text
ghcr.io/asibackbone/netcoreapplicationtemplate
```

Stable tags publish the full version tag, the major tag, and `latest`. Prerelease tags publish only the full version tag.

The publish job uses the `container-publish` GitHub environment. Keep deliberate maintainer approval on that environment so every production GHCR publication retains a manual approval gate. Approve both protected publish jobs within their documented coordination window so the evidence set can be completed.

See [Container Release Publishing](container-publish.md) for details.

## Dependency Update Automation

The repository uses Dependabot to monitor supported dependency ecosystems.

Dependabot is configured in `.github/dependabot.yml` for:

- NuGet packages used by project files.
- Docker base images used by the `Dockerfile`.
- GitHub Actions used by workflow files.

The `Dockerfile` pins both base images by digest. A digest never drifts, so a patched base image is only picked up when the pin is updated; the `docker` ecosystem is what opens that pull request.

Dependabot runs weekly on Monday morning in the `America/Chicago` timezone.

## Dependency Grouping

NuGet updates are grouped by related package families where practical:

- Microsoft ASP.NET Core packages.
- Microsoft Entity Framework Core packages.
- Serilog packages.
- OpenTelemetry packages.
- Test dependencies.
- External authentication dependencies.

Docker base image updates are grouped as `dotnet-base-images` so an SDK and runtime image bump arrives as one pull request.

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

## Project Automation Credential

The project-status workflows use a dedicated GitHub App installation token rather
than a classic personal access token. The installation token is created at run
time and expires after one hour; `actions/create-github-app-token` also revokes
the token when the job completes.

The built-in `GITHUB_TOKEN` is intentionally not used for these mutations.
`GITHUB_TOKEN` is repository-scoped and does not provide the organization-level
Projects v2 permission required by `updateProjectV2ItemFieldValue` and
`addProjectV2ItemById`.

The workflow-level `permissions:` block controls only the built-in
`GITHUB_TOKEN`. It does not constrain a GitHub App installation token or any
other external credential. The two project automation workflows therefore set
`permissions: {}` and explicitly request the GitHub App token permissions they
need.

### GitHub App configuration

Create a dedicated GitHub App for NCAT project automation and install it only on
the `AsiBackbone` organization with repository access limited to
`NetCoreApplicationTemplate`.

Grant the App only these permissions:

- Organization permissions:
  - **Projects: Read and write**
- Repository permissions:
  - **Issues: Read-only**
  - **Pull requests: Read-only**
  - **Metadata: Read-only** (GitHub grants this baseline permission)

The pull-request status workflow requests Projects write, Issues read, and Pull
requests read. The branch-status workflow requests only Projects write and
Issues read.

Configure these repository values:

| Type | Name | Purpose |
| --- | --- | --- |
| Repository variable | `PROJECT_APP_CLIENT_ID` | GitHub App client ID; this value is not secret. |
| Repository secret | `PROJECT_APP_PRIVATE_KEY` | PEM private key used only to mint short-lived installation tokens. |

Keep the existing project configuration variables:

- `PROJECT_OWNER`
- `PROJECT_NUMBER`
- `PROJECT_REVIEW_STATUS`
- `PROJECT_IN_PROGRESS_STATUS`

Do not store an installation access token as a repository secret. The workflows
mint a fresh token for each job and pass it directly to `gh` through `GH_TOKEN`;
the token value is never printed by repository scripts.

### Migration from `PROJECT_TOKEN`

After the GitHub App is installed and both project-status workflows have
completed successfully:

1. Delete the legacy `PROJECT_TOKEN` repository secret.
2. Revoke the classic personal access token in the account that issued it.
3. Confirm no organization or repository automation still depends on that PAT
   before removing any associated authorization.

There is no classic-PAT fallback in these workflows. If a GitHub App becomes
impractical, any future fallback must use a separately reviewed fine-grained PAT
limited to the required project and repository rather than restoring a broadly
scoped classic PAT.

### Rotation and revocation

Rotate the App private key by generating a new key in the GitHub App settings,
updating `PROJECT_APP_PRIVATE_KEY`, validating both automation workflows, and
then deleting the previous key from the App.

For emergency revocation, delete the active App private key or suspend/uninstall
the App installation. Existing installation tokens are short-lived and the token
created by `actions/create-github-app-token` is revoked automatically at job
completion by default.

Review the App installation and permissions whenever the project owner,
repository scope, or project automation mutations change. Expanding the App to
additional repositories or permissions requires the same security review as a
workflow permission expansion.
