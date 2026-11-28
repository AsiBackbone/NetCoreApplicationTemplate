# Dynamic Application Security Testing

CI runs an [OWASP ZAP](https://www.zaproxy.org/) scan against the application generated from the template package
built in the same workflow run. The scan was authorized by
[AsiBackbone Organization Discussion #73](https://github.com/orgs/AsiBackbone/discussions/73) and is tracked by
[issue #767](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/767).

The scan is currently in **rollout stage 1: report-only**. ZAP alerts are published for triage and never fail CI.

## What is scanned

The `template-smoke-test` job in `.github/workflows/ci.yml` packs the template, installs that `.nupkg`, scaffolds
`ContosoSecurityPortal`, builds and tests it, builds its Docker image, starts it with Docker Compose, and waits for
`http://localhost:8080/health/live`. On the Linux leg only, the ZAP step then scans that running instance before log
capture and Compose teardown.

Scanning the package-generated application exercises the distribution boundary and the externally observable ASP.NET
Core behavior that consumers receive: security headers, cookies, error handling, health endpoints, and the default
cookie authentication posture. It does not scan the repository source tree directly.

## Scope boundary

The scan is loopback-only and disposable by construction:

* The only target is `http://localhost:8080`. The generated `docker-compose.yml` publishes that port on `127.0.0.1`
  only, and ZAP reaches it through `--network host`.
* The ZAP context includes only `http://localhost:8080/.*`. The spider does not leave that context.
* Routes that can redirect to, or receive callbacks from, an external identity provider are excluded:
  `/External/*`, `/signin-*`, `/signout-*`, and `/Saml2*`. The default scaffold uses local cookie authentication, so no
  external provider is configured, but the exclusions remain in case that default changes.
* Excludes apply only to URLs the spider discovers. ZAP requests the context and spider seed URL directly and follows
  redirects for that first request, so the seeds are limited to anonymous pages that answer `200` without redirecting:
  `/Account/Login`, `/health/live`, and `/health/ready`. Never seed a route that can redirect.
* ZAP runs with `-silent`, which stops update checks and other unsolicited requests. Its own proxy listens on
  `127.0.0.1:8090`, because the application already holds port 8080 on the host network.
* After the scan, `scripts/Write-ZapDastSummary.ps1` checks every site and alert instance in the JSON report, and every
  URL in ZAP's session, exported as a plain URL list. **Any URL outside `http://localhost:8080` fails the job.** This
  scope guard is enforced now, even though alerts are not. It detects an out-of-scope request after the fact; the seed
  and exclusion rules above are what prevent one.
* Never point the plan at a shared, public, or production deployment. A scan of any other environment needs its own
  authorization.

## Scan configuration

The plan is checked in at `.zap/ncat-unauthenticated.yaml` and uses the ZAP Automation Framework. Stage 1 runs these
jobs:

| Job | Purpose | Bound |
|:---|:---|:---|
| `passiveScan-config` | Passive rules scan in-scope traffic only, with at most 10 alerts per rule | - |
| `requestor` | Requests the passive-scan canary (below), then seeds `/health/live` and `/health/ready`, which are not linked from rendered pages | - |
| `spider` | Starts at `/Account/Login` and discovers linked pages with GET requests. Forms are parsed but never submitted | 3 minutes, depth 5 |
| `passiveScan-wait` | Waits for passive analysis to finish | 3 minutes |
| `export` | Writes every URL in the session to `ncat-zap-urls.txt` for the scope guard | - |
| `report` | Writes `ncat-zap-report.json` and `ncat-zap-report.html` | - |

### Passive-scan canary

A report with no alerts is only meaningful if the passive scanner actually analyzed the traffic. The requestor therefore
first requests `/Account/Login?password=zap-passive-canary`. The login page ignores the synthetic parameter, but ZAP
rule 10024 (*Information Disclosure - Sensitive Information in URL*) always flags it. The summary script is called with
that URL as `-CanaryUrl` and **fails the job if rule 10024 raised no alert on it**. The canary appears in the report as a
single Informational alert. Triage records it as an intentional fixture, not a finding.

There is no active scan in stage 1. The whole container run is bounded by `timeout 12m`, and the step has a
15-minute `timeout-minutes` backstop. ZAP exit code `2` (plan warnings) is reported as a workflow warning. Any other
non-zero exit code, including a timeout, fails the job.

The image is the official ZAP stable image, `ghcr.io/zaproxy/zaproxy`, pinned by immutable digest in the `ZAP_IMAGE`
variable at the top of `ci.yml`. A trailing comment records the matching version. When updating it, resolve the new
`stable` digest and update the digest and the comment together.

## Results

The Linux smoke-test leg publishes:

* A **job summary** with the ZAP version, number of requested URLs, scope-guard result, canary result, scan duration, alert-type counts by risk,
  and one row per alert type.
* A **`dast-ncat-zap` artifact** with the JSON and HTML reports, the URL list, and the summary. It is kept for
  7 days and uploaded on both success and failure.

The workflow keeps `permissions: contents: read`. The scanner never creates or updates GitHub issues, and it never
uploads results to code scanning.

## Cleanup

The ZAP container runs with `--rm` and a fixed name, `ncat-zap`. A `Remove OWASP ZAP container` step runs with
`always()` and force-removes it. The existing `Stop scaffolded Docker Compose application` step also runs with
`always()` and removes the application containers and volumes. Both steps run after a successful, failed, timed-out,
or cancelled scan.

## Runner egress

The pinned image is pulled from GitHub Container Registry, so `ALLOWED_ENDPOINTS_SMOKE` includes `ghcr.io:443` and
`pkg-containers.githubusercontent.com:443`. Those are the only allow-list additions for the scan. Validate any image
change by dispatching CI with `egress_policy=block`, as described in
[Build Quality and Reproducibility](build-quality.md#runner-egress-policy).

## Local reproduction

Prerequisites: the .NET SDK from `global.json`, and Docker on Linux (or Docker Desktop with host networking enabled).
From the repository root:

```bash
dotnet pack ./eng/NetCoreApplicationTemplate.Template.csproj --configuration Release --output ./artifacts/template-package
dotnet new install ./artifacts/template-package/*.nupkg
dotnet new netcoreapp-template --name ContosoSecurityPortal --output ./artifacts/scaffold/ContosoSecurityPortal

(cd ./artifacts/scaffold/ContosoSecurityPortal && docker compose -p contososecurityportal-ci up --build --detach)
curl --fail http://localhost:8080/health/live

mkdir -p ./artifacts/dast/ncat && chmod 0777 ./artifacts/dast/ncat
ZAP_IMAGE="$(sed -n 's/^  ZAP_IMAGE: \([^ ]*\).*/\1/p' .github/workflows/ci.yml)"
docker run --rm --network host \
  --volume "$PWD/.zap:/zap/plans:ro" \
  --volume "$PWD/artifacts/dast/ncat:/zap/wrk:rw" \
  "$ZAP_IMAGE" zap.sh -cmd -silent -host 127.0.0.1 -port 8090 -autorun /zap/plans/ncat-unauthenticated.yaml

pwsh ./scripts/Write-ZapDastSummary.ps1 \
  -ReportPath ./artifacts/dast/ncat/ncat-zap-report.json \
  -UrlListPath ./artifacts/dast/ncat/ncat-zap-urls.txt \
  -CanaryUrl 'http://localhost:8080/Account/Login?password=zap-passive-canary'

(cd ./artifacts/scaffold/ContosoSecurityPortal && docker compose -p contososecurityportal-ci down --volumes --remove-orphans)
dotnet new uninstall NetCoreApplicationTemplate
```

Run `./scripts/Write-ZapDastSummary.ps1 -SelfTest` after changing the summary or scope-guard logic. CI runs it in the
`Build validation` job.

## Alert triage

Classify every alert in the report into exactly one category:

| Category | Meaning | Action |
|:---|:---|:---|
| Defect | The generated application behaves insecurely | Fix it in the template, with a test, through the normal issue and PR process. Report anything exploitable privately under `SECURITY.md` instead of in a public issue |
| Acceptable platform behavior | Intended ASP.NET Core or template behavior that the rule does not account for | Record a narrow filter (see below) |
| Scanner limitation | A false positive or a rule that cannot judge this response correctly | Record a narrow filter (see below) |
| Out of scope | The finding concerns something the scan does not assure, such as an excluded route | Document it. Do not widen the scan to chase it |

Filters must be narrow: one rule ID and one URL pattern each, checked in to `.zap/` with a rationale, an owner, and a
review condition. Never disable a whole rule or suppress by risk level.

## Rollout

1. **Report-only scan (current).** Bounded spider and passive analysis, reports, job summary, and scope guard.
2. **Triage.** Review and classify every stage-1 alert.
3. **Filters.** Record only the narrow rule-and-URL filters that triage justifies.
4. **Regression fixture.** Prove with a controlled insecure response that an unsuppressed policy-breaking alert fails
   the intended gate, then remove the fixture.
5. **Enforcement.** Fail on new unsuppressed Medium or High alerts. Low and Informational alerts stay in reports and
   the job summary.

A strictly time-bounded active scan may be added once stage 2 has confirmed the scope and exclusions. If DAST becomes
a required status check, update the repository security-control baselines in `eng/repository-controls` in the same
change.

## Limits of the assurance

This scan supports a narrow claim: an unauthenticated ZAP spider and passive analysis of the default package-generated
application, on loopback, found the reported alerts. It does not show that the application is secure.

In particular, the scan does not cover:

* authenticated pages, or any authorization rule behind the login;
* non-default template options such as other authentication providers or database providers;
* active attacks such as injection, until an active scan is added;
* production configuration, TLS, reverse proxies, or hosting infrastructure; or
* business logic, which needs threat modeling and manual review.

DAST complements rather than replaces threat modeling, authorization tests, code review, dependency and container
scanning, manual testing, and the repository [security process](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/SECURITY.md).
