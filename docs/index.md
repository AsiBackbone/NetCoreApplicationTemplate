---
title: "Secure-by-Default ASP.NET Core Starting Point"
description: Start an ASP.NET Core application with authentication, authorization, logging, telemetry, security headers, rate limiting, and EF Core data access in place.
_disableAffix: true
_disableBreadcrumb: true
_disableToc: true
---

# .NET Core Application Template Documentation

<div class="home-hero">
  <p class="home-kicker">Secure-by-default ASP.NET Core starting point</p>
  <p class="home-summary">Start an ASP.NET Core application with authentication, authorization, structured logging, telemetry, security headers, rate limiting, health checks, and EF Core data access already implemented and tested.</p>
  <p class="home-why">The .NET Core Application Template (NCAT) exists so teams do not rebuild the same security and operational foundations for every application. Use it when you are starting a long-lived, team-maintained ASP.NET Core web application or API that needs those foundations from the first commit.</p>
  <div class="home-actions" role="group" aria-label="Start with the template">
    <a class="btn btn-primary" href="articles/template-packaging.md">Get Started</a>
    <a class="btn btn-outline-primary" href="#is-the-template-a-fit">Is It a Fit?</a>
    <a class="btn btn-outline-primary" href="articles/getting-started.md">First Run</a>
  </div>
  <p class="home-boundary">This site is the implementation and operational reference for NCAT. It is authoritative for the template's generated behavior, template options, configuration, middleware order, authentication and authorization defaults, data-access implementation, public surface, ADRs, release compatibility, and runtime contracts.</p>
</div>

## Start here

<div class="entry-point-grid">
  <a class="entry-point" href="articles/template-packaging.md">
    <strong>Install and generate an application</strong>
    <span>Install the package, run <code>dotnet new</code>, choose template options, and validate the scaffold.</span>
  </a>
  <a class="entry-point" href="articles/getting-started.md">
    <strong>Build, test, and run it</strong>
    <span>Prerequisites, local build and test commands, first startup, and local documentation serving.</span>
  </a>
  <a class="entry-point" href="articles/authentication.md">
    <strong>Secure sign-in and access</strong>
    <span>Cookie authentication with authenticated-by-default routed endpoints, and how to change it safely.</span>
  </a>
  <a class="entry-point" href="articles/logging.md">
    <strong>Observe the application</strong>
    <span>Structured Serilog logging, with telemetry and health checks documented alongside.</span>
  </a>
  <a class="entry-point" href="articles/data-access.md">
    <strong>Work with data</strong>
    <span>EF Core with SQLite for local development or SQL Server, and audit records written on save.</span>
  </a>
  <a class="entry-point" href="articles/production-deployment-checklist.md">
    <strong>Deploy and operate</strong>
    <span>The production checklist, reverse proxy and container notes, and runtime readiness.</span>
  </a>
</div>

## Is the template a fit?

<div class="fit-grid">
  <section class="fit-panel" aria-labelledby="fit-use">
    <h3 id="fit-use">Use the template when</h3>
    <ul>
      <li>you are starting a long-lived web application or API that a team will maintain and operate;</li>
      <li>you want secure defaults, structured logging, telemetry, health checks, and data access configured and tested from day one; and</li>
      <li>you want a documented <a href="articles/public-surface-v1.md">public surface</a> and middleware order to extend rather than reinvent.</li>
    </ul>
    <p>If you choose the template, keep its secure defaults. Each reference article explains what a default protects before you change it.</p>
  </section>
  <section class="fit-panel" aria-labelledby="fit-simpler">
    <h3 id="fit-simpler">When this may be unnecessary</h3>
    <ul>
      <li>For a prototype, demo, or small single-purpose service, the built-in <code>dotnet new web</code>, <code>webapi</code>, or <code>webapp</code> templates give a smaller starting point you can grow deliberately.</li>
      <li>If your organization already has a platform template, adopt individual patterns instead, using articles such as <a href="articles/security-headers.md">Security Headers</a>, <a href="articles/rate-limiting.md">Rate Limiting</a>, and <a href="articles/forwarded-headers.md">Forwarded Headers</a> as references.</li>
      <li>If framework authorization and a simple service are enough, see <a href="https://asibackbone.github.io/Learning/architecture/when-aspnet-core-authorization-is-enough.html">When ASP.NET Core Authorization Is Enough</a> and <a href="https://asibackbone.github.io/Learning/architecture/when-a-simple-application-service-is-enough.html">When a Simple Application Service Is Enough</a>.</li>
    </ul>
    <p>Not every application needs every feature the template provides.</p>
  </section>
</div>

## At a glance

| Area | Current reference |
|:---|:---|
| Current release | [Latest GitHub release](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/latest) |
| Supported platform | .NET 10.0 |
| Template package | [NetCoreApplicationTemplate on NuGet](https://www.nuget.org/packages/NetCoreApplicationTemplate) |
| Template short name | `netcoreapp-template` |
| Default authentication | Cookie authentication with authenticated-by-default routed endpoints |
| Default data provider | SQLite for local development |

## Understand the generated application

- [Project Structure](articles/project-structure.md) explains the generated projects and responsibilities.
- [Configuration](articles/configuration.md) documents the application-owned configuration surface.
- [Middleware Pipeline](articles/middleware.md) records the runtime ordering contract and extension points.

## Generated application reference

Use these articles when you need the exact behavior of the scaffold rather than general ASP.NET Core guidance:

- [Authentication](articles/authentication.md)
- [Authorization](articles/authorization.md)
- [Error Handling](articles/error-handling.md)
- [Logging](articles/logging.md)
- [Telemetry](articles/telemetry.md)
- [Security Headers](articles/security-headers.md)
- [Forwarded Headers](articles/forwarded-headers.md)
- [Rate Limiting](articles/rate-limiting.md)
- [Health Checks](articles/health-checks.md)
- [API Versioning](articles/api-versioning.md)
- [Data Access](articles/data-access.md)

The [Public Surface and Compatibility Boundaries](articles/public-surface-v1.md) article records the generated configuration, routes, template symbols, middleware ordering, and other behavior consumers may rely on.

## Extensibility

- [Optional Application and Domain Layers](articles/optional-application-domain-layers.md)
- [Public Surface and Compatibility Boundaries](articles/public-surface-v1.md)

These references identify the intended growth points and the behavior that should remain stable when extending a generated application.

## Deployment & operations

- [Deployment Notes](articles/deployment.md)
- [Docker Development](articles/docker.md)
- [Production Deployment Checklist](articles/production-deployment-checklist.md)
- [Runtime Readiness](articles/runtime-readiness.md)

## Architecture decisions and API reference

Repository-local design decisions remain first-class NCAT documentation because they explain why this implementation chose its concrete behavior.

- [Architecture Decision Records](adr/index.md)
- [API Reference](api/index.md)

## Release & compatibility

Release and readiness evidence remains available after the core usage and implementation reference:

- [Build Quality and Reproducibility](articles/build-quality.md)
- [Container Release Publishing](articles/container-publish.md)
- [GitHub Workflow](articles/github-workflow.md)
- [Test Coverage](https://asibackbone.github.io/NetCoreApplicationTemplate/coverage/)

## Historical archive

- [v0.5.x to v1.0 Migration Guide](articles/v1-migration-guide.md) — retained at its original URL for consumers of preview releases; it is not current upgrade guidance.

## Learn the architecture

For organization-level software architecture education, ASP.NET Core teaching, terminology, tutorials, tradeoff analysis, architectural comparisons, labs, and general secure-by-default guidance, use [AsiBackbone Learning](https://asibackbone.github.io/Learning/).

Learning provides broader educational context; it does **not** define NCAT runtime behavior. See [Documentation Ownership](articles/documentation-ownership.md) for the cross-repository ownership contract.

## Related projects

Neither related project is a dependency of the template. Generated applications do not reference any AsiBackbone package. If a host later adopts AsiBackbone, [Audit Accountability Integration](articles/audit-accountability-integration.md) describes the host-owned integration seams.

<div class="project-grid">
  <article class="project-card" aria-current="page">
    <span class="project-role">Start a secure application</span>
    <h3>.NET Core Application Template <span class="current-site">Current site</span></h3>
    <p>A secure-by-default ASP.NET Core application template with authentication and authorization, structured logging, security headers, rate limiting, EF Core data access, reverse proxy support, and Problem Details error handling.</p>
    <p class="project-links">
      <a href="https://github.com/AsiBackbone/NetCoreApplicationTemplate" aria-label=".NET Core Application Template repository">Repository</a>
      <a href="https://doi.org/10.5281/zenodo.20373042" aria-label=".NET Core Application Template archive (DOI)">Archive (DOI)</a>
    </p>
  </article>
  <article class="project-card">
    <span class="project-role">Learn the patterns</span>
    <h3><a href="https://asibackbone.github.io/Learning/">AsiBackbone Learning</a></h3>
    <p>Practical .NET architecture tutorials, labs, runnable samples, and reference patterns for governed execution, secure applications, AI integration, and policy-driven systems, including comparisons that show when these patterns are unnecessary.</p>
    <p class="project-links">
      <a href="https://asibackbone.github.io/Learning/" aria-label="AsiBackbone Learning documentation">Documentation</a>
      <a href="https://github.com/AsiBackbone/Learning" aria-label="AsiBackbone Learning repository">Repository</a>
      <a href="https://doi.org/10.5281/zenodo.21938556" aria-label="AsiBackbone Learning archive (DOI)">Archive (DOI)</a>
    </p>
  </article>
  <article class="project-card">
    <span class="project-role">Add governed decisions</span>
    <h3><a href="https://asibackbone.github.io/AsiBackbone/">AsiBackbone</a></h3>
    <p>A .NET governance and policy-control framework for auditable decision flows: policy evaluation, auditable decision records, acknowledgment workflows, capability-based authorization, host-owned execution boundaries, and ASP.NET Core integration.</p>
    <p class="project-links">
      <a href="https://asibackbone.github.io/AsiBackbone/" aria-label="AsiBackbone documentation">Documentation</a>
      <a href="https://github.com/AsiBackbone/AsiBackbone" aria-label="AsiBackbone repository">Repository</a>
      <a href="https://doi.org/10.5281/zenodo.20546032" aria-label="AsiBackbone archive (DOI)">Archive (DOI)</a>
    </p>
  </article>
</div>

## Design philosophy

<div class="philosophy-callout">
  <p class="philosophy-statement">We favor architectures where consequential operations are explicit, constrained, reviewable, and auditable.</p>
  <p>We also document when these patterns are unnecessary: see <a href="#is-the-template-a-fit">When this may be unnecessary</a>, <a href="https://asibackbone.github.io/Learning/architecture/when-aspnet-core-authorization-is-enough.html">When ASP.NET Core Authorization Is Enough</a>, and <a href="https://asibackbone.github.io/Learning/architecture/when-a-simple-application-service-is-enough.html">When a Simple Application Service Is Enough</a>.</p>
  <p>The projects focus on practical .NET engineering. They do not claim regulatory certification or automatic compliance.</p>
</div>
