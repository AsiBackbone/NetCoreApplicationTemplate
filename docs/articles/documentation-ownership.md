# Documentation Ownership

`AsiBackbone/NetCoreApplicationTemplate` (NCAT) and `AsiBackbone/Learning` have complementary documentation roles. The goal is to keep NCAT precise about the implementation consumers actually run while keeping reusable architecture education in one canonical educational repository.

## Ownership Contract

- **ASI Backbone Learning** is the canonical educational source for organization-level software architecture education, ASP.NET Core teaching, terminology lineage, tutorials, tradeoff analysis, architectural comparisons, labs, and general secure-by-default guidance.
- **NCAT** is authoritative for the concrete template implementation: installation, template options, generated structure, runtime defaults, configuration, authentication and authorization behavior, exact middleware order, data-access implementation, deployment, extensibility, API/reference surface, ADRs, releases, compatibility, migration guidance, and operational behavior.

This separation does not reduce NCAT documentation. NCAT should remain detailed wherever a consumer needs to know **what the template does, what a specific version guarantees, or why this repository made a local architectural choice**.

Learning can explain the broader principle and alternatives, and it can use NCAT as a working reference implementation. Learning does **not** define NCAT runtime behavior.

## Ownership Matrix

| Documentation type | Source of truth |
|:---|:---|
| General ASP.NET Core architecture education | Learning |
| Middleware-ordering principles and alternatives | Learning |
| Secure-configuration principles | Learning |
| Logging and error-handling education | Learning |
| General EF Core boundary and transaction reasoning | Learning |
| ADR methodology and lifecycle education | Learning |
| Architecture comparisons and tradeoffs | Learning |
| Tutorials, labs, and terminology lineage | Learning |
| Template installation and CLI usage | NCAT |
| Template parameters and generated output | NCAT |
| Exact middleware order in generated applications | NCAT |
| Concrete authentication and authorization defaults | NCAT |
| Concrete configuration keys and options | NCAT |
| EF Core implementation and migration behavior | NCAT |
| Package and template public surface | NCAT |
| Deployment and runtime behavior | NCAT |
| Repository ADRs documenting local decisions | NCAT |
| Release, compatibility, migration, and quality evidence | NCAT |

## How to Handle Topics That Span Both Repositories

Some topics naturally need both a general lesson and an implementation reference. In those cases:

1. **Learning teaches the reusable idea.** Explain the principle, terminology, alternatives, tradeoffs, and cases where another design may be preferable.
2. **NCAT documents the concrete choice.** State the exact generated behavior, configuration, ordering, defaults, limitations, and operational consequences for this repository.
3. **Cross-link rather than duplicate.** NCAT can summarize the local rationale and point to Learning for the broader lesson. Learning can point to NCAT as a fuller working specimen.
4. **Keep runtime authority local.** If an educational explanation and the current NCAT implementation ever diverge, NCAT source, generated output, versioned release artifacts, local ADRs, and NCAT implementation documentation define NCAT behavior.

Examples:

| Topic | Learning should cover | NCAT should cover |
|:---|:---|:---|
| Middleware ordering | Why ordering matters, common failure modes, and alternative pipeline designs | The exact NCAT middleware sequence and local rationale |
| Secure defaults | General principles, threat boundaries, and tradeoffs | The defaults NCAT actually emits and how to configure or opt out of them |
| Error handling | Problem Details concepts, exception-boundary patterns, and alternatives | NCAT handlers, response behavior, logging integration, and configuration |
| EF Core | Boundary, transaction, lifetime, and persistence reasoning | NCAT DbContext registration, providers, migrations, interceptors, and save behavior |
| ADRs | ADR purpose, lifecycle, review methods, and alternatives | Repository decisions under `docs/adr` and their effect on NCAT |

## Contributor Routing Rule

Ask one question first:

> **Would this page still be useful if NCAT did not exist?**

- If **yes**, it probably belongs in Learning.
- If **no** because it describes a concrete NCAT option, generated behavior, version, configuration key, local decision, or operational contract, it belongs in NCAT.
- If the answer is **both**, split the reusable lesson from the implementation reference and cross-link them.

## Canonical Links

- [ASI Backbone Learning — published educational site](https://asibackbone.github.io/Learning/)
- [AsiBackbone/Learning — source repository](https://github.com/AsiBackbone/Learning)
- [NetCoreApplicationTemplate — source repository](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

This ownership contract concerns documentation and information architecture only. It does not change NCAT runtime, template, package, public-surface, or release behavior.
