# AGENTS.md

## Purpose

This repository contains a reusable, production-oriented .NET application template for building secure, maintainable, and extensible ASP.NET Core applications.

AI coding agents working in this repository should treat the project as a reusable template package, not as a single application. Changes should preserve template reliability, security posture, documentation quality, and generated-project behavior.

## Technology Baseline

* Target the current repository baseline: .NET 10.0.
* Preserve ASP.NET Core conventions already used in the project.
* Preserve existing GitHub Actions, NuGet packaging, DocFX documentation, and template scaffolding behavior unless the issue explicitly requests changes.
* Prefer existing project patterns over introducing new dependencies or architectural styles.

## General Rules

* Keep changes small, focused, and directly related to the assigned issue.
* Do not make broad architectural changes unless explicitly requested.
* Do not change public APIs, template options, package metadata, or generated project behavior unless the issue clearly requires it.
* Do not remove tests, weaken assertions, or reduce coverage.
* Do not bypass or weaken CI, coverage gates, security checks, or release validation.
* Do not introduce secrets, credentials, tokens, private URLs, or environment-specific values.
* Avoid cosmetic churn in unrelated files.

## Security Expectations

Security defaults are part of the value of this template.

Agents must not weaken:

* Centralized exception handling.
* Problem Details behavior.
* Secure production error responses.
* Security headers.
* Rate limiting.
* Forwarded headers handling.
* Authentication and authorization foundations.
* Logging safeguards.
* Data access safety patterns.

Production error responses must not expose stack traces, raw exception details, connection strings, tokens, secrets, or other sensitive implementation details.

## Testing Expectations

When changing behavior, add or update tests.

Before finalizing work, run the relevant test suite when possible.

The repository root contains both a solution file and a project file, so validation commands should target the solution explicitly.

Preferred validation:

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

For coverage-related work, preserve or improve the configured coverage gate.

Tests should verify behavior, not implementation details, where practical.

## Documentation Expectations

Update documentation when changes affect:

* Template usage.
* Package installation.
* Generated project behavior.
* Public APIs.
* Security posture.
* Configuration.
* Release process.
* Badges or project metadata.

Common documentation files to consider:

* `README.md`
* `PACKAGE-README.md`
* `CHANGELOG.md`
* `docs/`
* `.github/`
* `CITATION.cff`

Do not update release numbers, package versions, DOI references, or changelog release entries unless the issue specifically asks for release-related work.

## Pull Request Expectations

Pull requests should be reviewable and focused.

Each PR should include:

* A clear summary of what changed.
* Tests added or updated, when applicable.
* Documentation updates, when applicable.
* Any compatibility, security, or migration notes.
* Confirmation that build/test validation was run, or a clear explanation if it was not.

## Dependency Guidance

* Avoid adding new dependencies unless necessary.
* Prefer built-in .NET and ASP.NET Core features when they meet the requirement.
* If adding a dependency, explain why it is needed and whether it affects the generated template.

## Repository-Specific Priorities

This project prioritizes:

1. Secure defaults.
2. Predictable template generation.
3. Maintainable architecture.
4. Clear documentation.
5. Reliable CI and release validation.
6. Strong test coverage.
7. Minimal surprise for downstream users.

When in doubt, choose the smallest safe change that satisfies the issue.
