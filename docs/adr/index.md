# Architecture Decision Records

Architecture Decision Records (ADRs) document long-term architectural decisions made for the .NET Core Application Template.

ADRs are intended to explain why a decision was made, what alternatives were considered, and what consequences the decision introduces. They are not meant to replace code comments, API documentation, or implementation guides.

## Ownership and Learning Boundary

The ADR files in this directory are the **authoritative repository-local records** for architectural decisions made by NetCoreApplicationTemplate (NCAT). For each NCAT decision, the canonical ADR records the repository-specific context and constraints, alternatives considered, decision, consequences, status or supersession relationship, implementation references, and any repository-specific review triggers.

[ASI Backbone Learning](https://asibackbone.github.io/Learning/) is authoritative for reusable ADR education: what ADRs are, why they are useful, how lifecycle review/deprecation/supersession works, how to read a decision chain, and how to distinguish reusable architectural principles from one repository's local choices.

Use these Learning resources for broader guidance and interpretation:

- [Architecture Decision Records Preserve Architectural Reasoning](https://asibackbone.github.io/Learning/aspnetcore/architecture-decision-records-preserve-architectural-reasoning.html)
- [Architecture Decision Record Lifecycle, Review, Deprecation, and Supersession](https://asibackbone.github.io/Learning/aspnetcore/architecture-decision-record-lifecycle-review-deprecation-and-supersession.html)
- [Working Repository ADR Case Study: NetCoreApplicationTemplate](https://asibackbone.github.io/Learning/aspnetcore/netcoreapplicationtemplate-adr-case-study.html)

Learning may use NCAT ADRs as teaching material, but an educational explanation or case study does **not** supersede, amend, or redefine the actual repository decision. When the two need to be compared, use the ADR file in this directory as the source of truth for what NCAT decided.

The contributor-facing mechanics below remain local to NCAT because maintainers need them to create and maintain this repository's ADR history consistently.

## When to Add an ADR

Add an ADR when a decision affects the long-term shape of the template, such as:

- Application startup and middleware organization.
- Logging, telemetry, or observability strategy.
- Security defaults and production guardrails.
- Authentication or authorization architecture.
- Data access provider strategy.
- Template packaging conventions.
- Testing strategy or repository workflow.
- Any decision that future maintainers may reasonably question.

Small implementation details, routine refactors, and temporary fixes usually do not need ADRs.

## Numbering Convention

ADR files use a four-digit, zero-padded sequence number followed by a short kebab-case title:

```text
0001-use-structured-serilog-logging.md
0002-use-centralized-application-middleware-pipeline.md
0003-example-future-decision.md
```

Rules:

- Assign the next available number when creating a new ADR.
- Do not renumber existing ADRs after they are merged.
- Keep filenames lowercase and use hyphens between words.
- Keep titles short but specific.
- Use the ADR template in [`template.md`](template.md).

## ADR Status Values

Use one of these status values:

| Status | Meaning |
|:---|:---|
| `Proposed` | Under consideration but not yet accepted. |
| `Accepted` | Current architectural decision. |
| `Deprecated` | No longer recommended, but not directly replaced. |
| `Superseded` | Replaced by a newer ADR. |

When an ADR is superseded, keep the original file and add a link to the replacing ADR. Do not delete historical ADRs unless they were created by mistake and have not been relied on.

## Current ADRs

| ADR | Title | Status |
|:---|:---|:---|
| [0001](0001-use-structured-serilog-logging.md) | Use structured Serilog logging | Accepted |
| [0002](0002-use-centralized-application-middleware-pipeline.md) | Use centralized application middleware pipeline | Accepted |
| [0003](0003-record-release-surface-and-distribution-strategy.md) | Record Release Surface and Distribution Strategy | Accepted |
| [0004](0004-keep-composite-savechanges-interceptor.md) | Keep the Composite EF Core SaveChanges Interceptor | Accepted |
