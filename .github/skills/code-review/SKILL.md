---
name: code-review
description: Review NetCoreApplicationTemplate repository and generated-template changes. Use this when reviewing pull requests, implementing issues, checking template contracts, evaluating security or middleware changes, or assessing compatibility and cross-repository documentation alignment.
---

# NetCoreApplicationTemplate Code Review

Use this skill to review or modify NetCoreApplicationTemplate (NCAT) with the repository's template, security, compatibility, documentation, and cross-repository boundaries in mind.

## Repository role

Treat NCAT as a reusable, production-oriented ASP.NET Core application template.

NCAT owns:

- template installation and options;
- generated application structure;
- middleware ordering;
- concrete configuration keys and defaults;
- authentication and authorization defaults;
- EF Core implementation and migrations;
- deployment and runtime behavior;
- repository ADRs;
- package and public template surface;
- release compatibility and generated-contract behavior.

Learning owns reusable architecture education. AsiBackbone owns its own governance package, API, runtime, migration, and integration contracts.

Do not introduce an AsiBackbone dependency merely because NCAT shares architectural terminology or aligns conceptually with AsiBackbone.

## Classify the changed surface first

Before evaluating a change, determine whether it affects one or more of these surfaces:

- repository-only tooling or documentation;
- template source;
- generated application output;
- scaffold manifest or overlay behavior;
- package identity or template metadata;
- runtime or security behavior;
- configuration contracts;
- authentication/database variant behavior;
- release evidence or historical records.

Do not assume a repository-only file is emitted into generated applications. Confirm the template path and scaffold behavior before treating repository content as part of the generated contract.

## Review generated-template compatibility

For template-affecting changes, verify the effects on:

- generated file presence and contents;
- template options and conditional output;
- scaffold manifests and overlays;
- all supported authentication/database combinations when relevant;
- package lock and restore behavior;
- target framework;
- package IDs, template identity, group identity, and short name;
- future generated applications versus applications generated from older NCAT releases.

Keep this distinction explicit:

- updating NCAT changes the template used for future generated projects;
- existing generated applications are independent copies and are not updated automatically.

Treat a change to generated structure, option behavior, defaults, or public template metadata as a compatibility concern even when the repository build itself still succeeds.

## Preserve secure defaults

Apply extra scrutiny when a change affects:

- middleware ordering;
- authentication or authorization;
- antiforgery;
- forwarded headers;
- rate limiting;
- security headers;
- centralized error handling;
- logging or telemetry;
- Data Protection;
- EF Core persistence;
- audit, reconciliation, or outbox behavior;
- health checks;
- configuration validation.

Prefer secure-by-default behavior with deliberate, documented opt-outs.

Do not weaken a production default merely to simplify a sample, test, local workflow, or migration.

When a security-sensitive behavior changes, check whether documentation, tests, generated configuration, or migration guidance must change with it.

## Respect documentation ownership

Use the repository's ownership rule before adding or expanding documentation:

- general architecture principle, comparison, tradeoff, tutorial, or teaching pattern -> Learning;
- exact NCAT generated, configured, exposed, guaranteed, or version-specific behavior -> NCAT;
- exact AsiBackbone package, API, runtime, migration, or integration behavior -> AsiBackbone.

Cross-repository links may support NCAT documentation, but they must not replace NCAT's own authoritative documentation for template options, generated contracts, configuration keys, ADR decisions, or runtime behavior that NCAT consumers rely on.

Preserve published NCAT documentation URLs when repository policy requires continuity.

## Preserve historical and release evidence

Distinguish current guidance from:

- release notes;
- completed alignment reviews;
- ADRs;
- immutable tags and GitHub Releases;
- provenance and release-evidence artifacts.

For historical cross-repository reviews, pin the matching release tag or exact reviewed commit rather than mutable `main` or a live documentation site that can drift.

Do not rewrite historical evidence merely to make it read like current guidance.

When wording becomes stale around an immutable artifact, prefer updating the surrounding evergreen documentation unless the repository's release process explicitly permits changing the artifact.

## Keep optional integrations optional

For AsiBackbone or any other optional integration:

- verify that a real generated or runtime integration surface exists before proposing dependency, migration, or compatibility work;
- do not infer a compile-time dependency from shared terminology or architectural alignment;
- preserve independently versioned product contracts;
- use versioned external contract vectors, release tags, or exact commits when an integration depends on them;
- distinguish stable compatibility baselines from `main` branches used only for drift detection.

Do not present reference or sample integration code as a required NCAT runtime dependency.

## Validate against the affected surface

Use the repository-prescribed validation in `CONTRIBUTING.md`.

For runtime code changes, start with:

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

For formatting-sensitive changes, use:

```powershell
dotnet format --verify-no-changes --verbosity minimal
```

For template or packaging changes, also run the repository's scaffold, manifest, package, and supported authentication/database combination checks that cover the changed surface.

For documentation changes:

- confirm navigation when applicable;
- validate links;
- build the documentation when appropriate;
- verify examples remain safe and accurate;
- confirm runtime behavior was not changed unintentionally.

Do not claim a command, check, or CI gate passed unless it was actually run or there is authoritative CI evidence.

## Prioritize meaningful review findings

Prioritize findings involving:

- generated-output drift;
- security regressions;
- middleware ordering;
- configuration contract changes;
- template/package identity changes;
- missing authentication/database variant coverage;
- release or SemVer impact;
- repository-only versus generated-output ambiguity;
- broken documentation ownership;
- stale mutable links in historical records;
- optional-integration boundary violations.

Avoid generic style comments unless they materially affect correctness, maintainability, security, compatibility, or an explicit repository standard.

When reviewing a pull request, explain the concrete downstream effect of each finding and identify the repository rule, contract, or validation surface that makes the finding important.
