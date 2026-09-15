# Workflow Hardening and Execution Budgets

NetCoreApplicationTemplate uses `step-security/harden-runner` as a runtime
observation layer on CI and release jobs that restore dependencies, build
consumer-controlled content, invoke container tooling, or publish public
artifacts.

The current policy intentionally uses:

```yaml
egress-policy: audit
```

This is a deliberate baseline rather than an accidental lack of enforcement.
The repository has multiple release paths whose legitimate network destinations
include GitHub APIs and artifact storage, NuGet, .NET SDK/package distribution,
container registries, Trivy databases, Sigstore services, and transient
GitHub-hosted storage endpoints. A prematurely incomplete blocking allowlist
could fail a stable release after approval or, worse, encourage broad wildcard
exceptions that weaken the intended control.

Audit mode must therefore be treated as an endpoint-discovery phase. Moving a
protected publishing job to `egress-policy: block` requires review of successful
cold-cache and warm-cache runs and a versioned allowlist that covers every
required endpoint without catch-all entries.

## Hardened workflow coverage

The baseline applies Harden-Runner to:

- `build-validation`, because it restores packages, executes repository build
  scripts, runs tests, restores local tools, and invokes third-party actions;
- every `template-smoke-test` operating-system leg, because those jobs install
  the produced template, restore generated projects, and on Linux build and run
  containers;
- `template-option-matrix`, because it restores and builds generated consumer
  combinations;
- `pack-template`, because release inputs are restored and built before the
  package becomes eligible for publication;
- `publish-template`, because it receives OIDC publishing authority and writes
  release evidence and the public package;
- both container release jobs, which already use Harden-Runner while building,
  scanning, publishing, signing, attesting, and writing release evidence.

The dependency-review job remains read-oriented and does not restore or execute
the application dependency graph. Other narrowly scoped validation jobs should
be evaluated when their permissions or executable inputs materially expand.

## Outbound endpoint inventory

The following endpoint classes are expected during CI and release operations.
They are documentation for audit review, not yet a copy-and-paste blocking
allowlist because some GitHub-hosted artifact and CDN destinations are dynamic.

| Purpose | Expected endpoint classes / representative hosts |
| --- | --- |
| GitHub source, API, release and artifact operations | `github.com`, `api.github.com`, `*.githubusercontent.com`, `*.actions.githubusercontent.com` |
| GitHub OIDC and attestations | `token.actions.githubusercontent.com`, GitHub API and attestation service endpoints surfaced by the pinned GitHub actions |
| NuGet restore and Trusted Publishing | `api.nuget.org`, `globalcdn.nuget.org`; `nuget.pkg.github.com` for the explicitly supported GitHub Packages path |
| .NET SDK/package acquisition | Microsoft .NET download/CDN endpoints surfaced by `actions/setup-dotnet` when the requested SDK is not already present |
| GHCR publication and pulls | `ghcr.io` plus the GitHub container storage endpoints returned by GHCR |
| Base image pulls during Docker builds | Microsoft Container Registry and Docker registry/auth/CDN endpoints required by the repository Dockerfile |
| Trivy vulnerability database and scanner support | Aqua Security/GHCR endpoints used by the pinned `aquasecurity/trivy-action` |
| Keyless container signing | GitHub OIDC plus Sigstore Fulcio, Rekor, and trusted-root/TUF distribution endpoints used by Cosign |
| GitHub-hosted artifact/cache transfer | Ephemeral GitHub Actions/Azure-backed storage hosts issued to the current workflow run |

When audit output shows a previously undocumented destination, determine which
step initiated it before adding it to this inventory. Unexpected endpoints are a
security signal, not an automatic allowlist update.

## Criteria for enforcing block mode

Change a protected publishing job from audit to block only after:

1. observing multiple successful cold-cache and warm-cache runs for the exact
   release path;
2. exercising NuGet Trusted Publishing, GitHub release uploads, attestations,
   GHCR publication, Trivy database retrieval, and Cosign signing;
3. accounting for dynamic GitHub artifact/cache storage without introducing a
   broad unrestricted wildcard;
4. validating the proposed allowlist in a release-candidate or otherwise
   non-production publication path;
5. documenting each allowed destination by purpose; and
6. confirming `actionlint`, zizmor, CI, package publication, signing, and
   release-evidence verification still succeed.

Until those conditions are met, `audit` is preferred over a brittle `block`
configuration. The goal is enforceable least privilege, not a policy that
appears strict while requiring emergency bypasses during normal releases.

## Execution budgets

Long-running jobs have explicit ceilings so a stalled restore, container
operation, registry call, or release step cannot consume a runner indefinitely.

| Job | Timeout | Rationale |
| --- | ---: | --- |
| `build-validation` | 30 minutes | Allows cold restore, build, tests, tooling restore, and coverage generation with headroom. |
| `template-smoke-test` | 45 minutes | Cross-platform template generation plus Linux Docker build/start/health validation can be substantially slower on a cold runner. |
| `template-option-matrix` | 20 minutes | Existing bounded budget for one generated option combination. |
| `pack-template` | 25 minutes | Covers locked restore, solution build, package creation, SBOM generation, and artifact upload. |
| `publish-template` | 20 minutes | Covers approval-to-run publication work, evidence upload/verification, OIDC login, and registry push. |
| container `build-scan` | 35 minutes | Allows image build/export, Trivy database retrieval, two scans, SBOM generation, and artifact uploads. |
| container `publish` | 30 minutes | Allows image publication, digest resolution, Cosign signing, provenance, evidence assembly, uploads, and verification. |
