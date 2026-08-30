# Contributing

Thank you for helping improve the .NET Core Application Template.

This project is intended to remain a clean, reusable, production-oriented ASP.NET Core baseline. Contributions should preserve that goal by keeping changes focused, documented, and easy to review.

## Community Standards

Participation in this repository is governed by [COMMUNITY_STANDARDS.md](COMMUNITY_STANDARDS.md).

Contributors should keep discussions respectful, focused on the project scope, and safe for public review.

## Contribution Principles

Contributions should favor:

- Secure-by-default behavior.
- Clear middleware and startup organization.
- Maintainable configuration patterns.
- Strong validation and test coverage for runtime behavior.
- Documentation for decisions that affect future template users.
- Small pull requests with a clear issue relationship.

Avoid mixing unrelated work in a single pull request. Documentation-only, dependency-only, test-only, and runtime behavior changes should usually be kept separate.

## Issue Workflow

The project backlog uses the following general flow:

| Status | Meaning |
|:---|:---|
| `Backlog` | Idea or task has been captured but is not ready for active work. |
| `Ready` | Issue is understood well enough to begin. |
| `In Progress` | Work has started on a branch. |
| `Blocked` | Work cannot continue until a decision, dependency, or fix is available. |
| `In Review` | Pull request has been opened and is waiting for validation or review. |
| `Done` | Pull request has been merged or the issue has otherwise been completed. |

Recommended flow:

1. Confirm the issue is still relevant.
2. Move or treat the issue as `Ready` before starting work.
3. Create a branch from the current `main` branch.
4. Keep commits focused on the issue.
5. Open a pull request when the work is ready for review.
6. Include `Closes #<issue-number>` when the pull request fully completes the issue.
7. Merge only after validation passes and the scope is reviewed.

If work is exploratory or only partially addresses an issue, use `Related to #<issue-number>` instead of a closing keyword.

## Issue Triage Expectations

Issues are triaged on a best-effort basis by the maintainer.

Triage considers:

- Whether the issue is reproducible from the repository or generated template output.
- Whether the issue is a security, release, packaging, documentation, or runtime concern.
- Whether the behavior belongs in the reusable template rather than in a downstream application.
- Whether the report includes enough details to act on.
- Whether the proposed change can be validated by CI, tests, documentation review, or release checklist review.

Issues may be closed when they are duplicated, stale, unreproducible, out of scope, missing required information, or specific to a consuming application's custom deployment.

## Branch Naming

Use short branch names that include the issue number and the type of work.

Recommended patterns:

```text
feature/issue-<issue-number>-short-description
fix/issue-<issue-number>-short-description
docs/issue-<issue-number>-short-description
refactor/issue-<issue-number>-short-description
test/issue-<issue-number>-short-description
chore/issue-<issue-number>-short-description
```

## Commit Messages

Use concise imperative commit messages. Start with a verb such as `Add`, `Fix`, `Update`, `Document`, `Configure`, or `Refactor`, and include the issue number when practical.

## Pull Request Expectations

Pull requests should include:

- A short summary of the change.
- A validation or testing section.
- A closing issue reference when the work completes an issue.
- Notes about behavior changes, migration steps, or deployment impact when relevant.

Documentation-only changes may use a lighter validation note that states the change was documentation-only and links were reviewed.

## Pull Request Triage Expectations

Pull requests are reviewed for scope, CI health, maintainability, and release impact.

Before merge, the maintainer reviews whether:

- The pull request matches the linked issue or stated purpose.
- Required checks pass.
- Documentation is updated when behavior, packaging, governance, or workflow expectations change.
- Security-sensitive changes preserve safe defaults.
- Template packaging changes preserve expected scaffolded output.
- Release, workflow, security, and governance changes receive deliberate maintainer review.

Pull requests may be returned for revision when they are too broad, mix unrelated changes, lack validation, bypass documented release expectations, or create unclear downstream behavior.

Pull requests targeting `main` require Code Owner review when files match `.github/CODEOWNERS`.

If new reviewable commits are pushed after approval, GitHub dismisses the stale approval and the pull request must be reviewed again before merge.

General required approval counts are not currently enabled while the repository is maintained under the solo-maintainer model. Required approvals may be enabled later when additional maintainers are added.

## Validation Expectations

For runtime code changes, run:

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

For formatting-sensitive changes, run:

```powershell
dotnet format --verify-no-changes --verbosity minimal
```

For documentation changes, validate that new pages are included in navigation when needed, links work, examples are safe for public use, and runtime behavior is not changed unintentionally.

## Documentation Expectations

Update NCAT documentation when a change affects configuration behavior, middleware ordering, security defaults, authentication, authorization, data access, deployment, GitHub workflow, release process, template packaging, generated output, or another concrete runtime or repository contract.

Long-term architectural decisions should be captured in an Architecture Decision Record under `docs/adr`.

### Choose the Documentation Repository

`AsiBackbone/Learning` and NCAT have complementary documentation responsibilities. Before adding a new conceptual explanation, tutorial, comparison, or reference page, choose the repository based on what must remain authoritative.

| Put documentation in... | When the material primarily... |
|:---|:---|
| [AsiBackbone/Learning](https://asibackbone.github.io/Learning/) | Teaches general software architecture or ASP.NET Core concepts; defines educational terminology; presents tutorials or labs; compares alternatives and tradeoffs; or explains secure-by-default principles that are useful beyond NCAT. |
| **NCAT** | Documents template installation or options; generated structure; exact middleware order; concrete configuration keys; authentication or authorization defaults; EF Core implementation or migrations; deployment/runtime behavior; repository ADRs; package/public surface; release compatibility; or other behavior a consumer must be able to rely on for a specific NCAT version. |

> **Documentation ownership rule:** If a document primarily explains a general software-architecture principle, tradeoff, comparison, or teaching pattern, it belongs in Learning. If it documents what NetCoreApplicationTemplate generates, configures, guarantees, exposes, or records as a repository decision, it belongs in NCAT.

When both repositories are relevant:

1. Put the reusable architectural lesson, alternatives, and tradeoff discussion in Learning.
2. Keep the NCAT page focused on the repository's concrete choice, exact behavior, configuration, and operational consequences.
3. Link from NCAT to Learning for the broader lesson instead of growing a duplicate educational treatment.
4. Link from Learning to NCAT when a working implementation is useful as a reference specimen.

Learning does not define NCAT runtime behavior. Do not make an educational page the sole source for a template option, configuration key, generated contract, local ADR decision, or version-specific runtime behavior that NCAT consumers must rely on.

Direct references:

- [ASI Backbone Learning — published site](https://asibackbone.github.io/Learning/)
- [AsiBackbone/Learning — source repository](https://github.com/AsiBackbone/Learning)
- [NCAT Documentation Ownership](docs/articles/documentation-ownership.md)

### Preserve Public Documentation URLs

Treat an established NCAT documentation URL as a compatibility surface. When reusable educational content moves to Learning, preserve the existing NCAT path whenever practical as an implementation-focused page or concise transition page rather than deleting it.

For documentation moves or major refactors:

1. Verify the canonical Learning destination exists before changing the NCAT page.
2. Preserve the existing NCAT URL when it has been published, linked from a README or ADR, referenced by Learning, or may be indexed/bookmarked externally.
3. Keep enough NCAT-specific content at that URL to state the current implementation contract and link outward for broader education.
4. Update inbound links only after the continuity path is in place.
5. Do not create a circular canonical-link pattern where an NCAT transition page sends readers to Learning and the Learning page sends them back to NCAT as its sole destination. Learning may link back to NCAT as an implementation specimen, while NCAT remains authoritative for runtime/template/ADR truth.
6. Keep release, migration, ADR, and other historical evidence available at stable paths or through an explicit archive/transition strategy.

The [Documentation Link Validation workflow](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/.github/workflows/link-validation.yml) builds the NCAT DocFX site, checks important NCAT-to-Learning links and generated reference surfaces, and validates selected Learning-to-NCAT backlinks. Documentation pull requests should keep that workflow green.

## Dependency Update Expectations

Dependency update pull requests should be reviewed like other pull requests. Confirm CI passes, review release notes for major updates, treat security updates as higher priority, and be careful with authentication, EF Core, middleware, logging, telemetry, and GitHub Actions updates.

## Branch Cleanup

After a pull request is merged, delete the remote branch unless it is intentionally kept for follow-up work.

For local cleanup:

```powershell
git fetch --prune
git branch --merged
```

## Security Notes

Do not commit private operational values or credentials. Use environment variables, user secrets, or approved secret stores for sensitive configuration.

## Solo-Maintainer Hardening Profile

This repository operates a solo-maintainer hardening profile through v1.x.

While the project has a single maintainer and no external contributors, the repository relies on protected branches, pull requests, CI validation, CodeQL, Dependency Review, Dependabot, explicit workflow permissions, and documented release/security procedures.

The following controls should be enabled before merging the first external pull request or before adding any repository collaborator:

- Signed commit requirement on protected branches.
- Required pull request reviewer approval.
- Code Owner review is required for pull requests targeting `main` when owned paths are changed.
- Stale pull request approvals are dismissed when new reviewable commits are pushed.
- General required approval counts remain deferred until additional maintainers are added.
- Review of branch protection settings for `main` and any long-lived development branch.
- Review of repository secrets, environment secrets, and package publishing permissions.

Trigger conditions for moving beyond the solo-maintainer profile include the first external pull request, first repository collaborator, first automated package publishing workflow, first automated container image publishing workflow, a confirmed credential exposure, or repeated high-risk dependency/security findings.

## GitHub Actions Supply-Chain Policy

GitHub Actions workflows should use explicit workflow or job-level permissions, default to read-only access unless more is required, use repository or environment secrets for publish credentials, pin actions to full commit SHAs when practical, and review action updates before merging.

## Related Documentation

- [Community Standards](COMMUNITY_STANDARDS.md)
- [Support Policy](SUPPORT.md)
- [Maintainers](MAINTAINERS.md)
- [Security Policy](SECURITY.md)
- [GitHub Workflow](docs/articles/github-workflow.md)
- [Configuration](docs/articles/configuration.md)
- [Deployment Notes](docs/articles/deployment.md)
- [Architecture Decision Records](docs/adr/index.md)
