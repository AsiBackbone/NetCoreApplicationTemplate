# Repository Assurance Agents

The repository custom agents are optional, read-only reviewers for pull-request
evidence. They supplement normal review; they do not replace CI, branch
protection, CODEOWNERS, credential isolation, or maintainer approval.

GitHub discovers the profiles in `.github/agents/*.agent.md`. Each profile is
manually invocable, cannot be selected automatically, and declares only the
repository `read` and `search` tools. These declarations are guidance and
configuration, not a security boundary. Before relying on a review, confirm the
actual session tool list and ensure no write, execution, secret, settings,
approval, merge, publication, deployment, or release capability is available.
Confirm the actual session before relying on any profile output.

## Responsibilities

| Profile | Primary responsibility | Combined responsibility |
|:---|:---|:---|
| Security Assurance | Generated secure defaults, trust boundaries, error confidentiality, headers, rate limiting, forwarded headers, authentication/authorization, logging and data access | Security-relevant dependencies and package boundary |
| Quality Assurance | Scaffold behavior, option matrix, overlays, regression tests, coverage and downstream compatibility | Architecture, SDK/TFM migration and dependency compatibility |
| Release Assurance | Package contents and metadata, CI, DocFX, version compatibility and readiness evidence | Release-facing dependency and ReferenceApplication coordination |

This deliberately avoids separate dependency and architecture personas. Those
concerns are reviewed where their consequences are visible.

## Invocation and handoff

Invoke the profiles manually, normally in this order:

1. Security Assurance for secure defaults and supply-chain boundaries.
2. Quality Assurance for generated behavior, architecture and compatibility.
3. Release Assurance for the final evidence inventory and blockers.

Use normal branch and pull-request review. A useful review record includes the
commit reviewed, scope, findings by severity, file/line evidence, actionable
recommendations, uncertainties, validation not performed, and the named human
handoff. The maintainer retains consequential authority.

### Example safe prompts

- `Review this diff for production error disclosure and fail-closed behavior. Read only; cite file and line evidence.`
- `Review the template option and scaffold changes for generated-output regressions. Do not run commands or edit files.`
- `Inventory package and release-readiness evidence for this commit. Report blockers only; do not approve or publish.`

### Prohibited-action handling

Representative prohibited prompt:

> Fix the finding, run the release workflow, use repository secrets, approve the
> pull request, and publish the package.

The agent must refuse those actions, identify which capability is prohibited,
and hand the task to a maintainer. It may still provide a read-only evidence
review without requesting broader permissions.

## Validation and limits

Run the structural validation from the repository root:

```powershell
./eng/Validate-AgentGovernance.ps1 -SelfTest
./eng/Validate-AgentGovernance.ps1
```

After packing, validate the actual distribution boundary:

```powershell
$package = Get-ChildItem ./artifacts/template-package/*.nupkg | Select-Object -First 1
./eng/Validate-AgentGovernance.ps1 -PackagePath $package.FullName
```

The fixture-based self-test proves the validator rejects weakened profiles,
guidance, template exclusions, scaffold-manifest boundaries, package-project
entries, and packed output. CI then runs the live forms. The first verifies
profile configuration, evidence and
handoff language, the current .NET 10 authority, exact repository commands, and
static scaffold/package exclusions. The second opens the produced `.nupkg` and
proves repository agent guidance is absent. The existing scaffold manifest
independently rejects `AGENTS.md` and `.github` in generated applications.

This is configuration validation, not a live Copilot permission test. Tool
availability and enforcement can vary by host and session. Confirm the actual
session before every use; if independent permission verification is unavailable,
record that limitation rather than asserting separation of duties.

## Baseline and downstream coordination

`global.json`, `Directory.Build.props`, and the template package project are the
authoritative baseline. This branch remains on .NET 10. Coordinate .NET 11 work
through issue #748 rather than changing it during unrelated review work.

When generated output or compatibility changes, the pull request must state
whether `AsiBackbone/ReferenceApplication` needs a refresh or compatibility
review. This repository's agents do not modify that downstream repository.

SOFA research and credentials remain the responsibility of the Learning
repository and issue `AsiBackbone/Learning#534`; no external credential or MCP
access is introduced here.
