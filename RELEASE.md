# Release Checklist and Runbook

Use this checklist before publishing a stable NetCoreApplicationTemplate package, GitHub Release, documentation update, Zenodo archive, or related release artifact.

This runbook is intentionally version-neutral. The current stable NuGet package identity is `NetCoreApplicationTemplate`; `CDCavell.NetCoreApplicationTemplate` is the legacy 1.0 package identity.

## 1. Release Branch Preparation

1. Confirm `main` is green in CI before creating the release branch.
2. Create a release branch from the current `main` head.
3. Confirm all planned release-readiness issues are merged before opening the release PR.
4. Avoid feature work on the release branch unless it directly addresses release validation, documentation, packaging, or blocking defects.
5. Treat the release branch as a short-lived preparation branch rather than a publishing boundary. Merge the completed release PR into protected `main` before creating any production `v*.*.*` tag.
6. Use a release-candidate tag, dry-run workflow, or pre-release GitHub Release before creating a DOI-bearing stable release when release automation, NuGet publication, Zenodo metadata, signing, SBOM generation, container publication, or documentation publication has changed since the previous release.
7. Do not retain the release branch after its pull request merges. The repository automatically deletes eligible merged head branches; if automatic deletion does not apply, delete the branch manually after confirming the merge is present on `main`.
8. Preserve release identity with the immutable `vMAJOR.MINOR.PATCH` tag, published GitHub Release, artifacts, and merged `main` commit. Never retain a merged release branch as the sole release record.

Example:

```powershell
git checkout main
git pull
git checkout -b release/vMAJOR.MINOR.PATCH
```

## 2. Release Gate Validation

Complete these checks before tagging a stable release:

- Confirm the [Repository Security Profile](docs/articles/repository-security-profile.md) still matches effective GitHub settings, including secret-scanning push protection, `main` branch protection, and required reviewers on `template-package-publish` and `container-publish`.
- Confirm the production release tag will point to the release commit after it is merged into `main`, not to an unmerged release branch commit.
- Confirm CI passes on the release branch.
- Confirm CodeQL/security scanning passes.
- Confirm dependency/audit scanning has no unresolved release-blocking findings.
- Run `./scripts/Validate-VersionConsistency.ps1`.
- Confirm the version in `Directory.Build.props`, `NetCoreApplicationTemplate.Template.csproj`, `CITATION.cff`, README examples, package documentation, and the latest `CHANGELOG.md` heading is aligned.
- Confirm tag/version agreement checks are part of the release gate.
- Run the release build quality commands documented in `docs/articles/build-quality.md`.
- Run the template smoke-test workflow against the release branch or release-candidate tag.
- Confirm scaffolded output matches `eng/scaffold-manifest.default.json`.
- Confirm scaffolded output contains no maintainer-only files.
- Confirm the template package ID remains `NetCoreApplicationTemplate` unless the release is intentionally changing package identity.

## 3. Artifact Confirmation

Before approving publication, review generated artifacts from the release workflow:

- `.nupkg`
- `.snupkg`, if produced
- Container image digest, if containers are published
- SBOM, if produced
- Provenance or attestation file, if produced
- Checksum files, if produced
- Package metadata
- Generated release notes
- GitHub Actions artifacts
- Generated documentation artifacts
- DOI / Zenodo archive record, when available

Confirm artifact names, versions, repository URLs, license metadata, authorship metadata, package descriptions, package IDs, and release notes are accurate before publication.

## 4. NuGet Publication Policy

The current public package identity is:

```text
NetCoreApplicationTemplate
```

The previous package identity is legacy:

```text
CDCavell.NetCoreApplicationTemplate
```

Current NuGet.org publication uses NuGet Trusted Publishing through GitHub Actions OIDC. Avoid introducing long-lived NuGet API keys for normal NuGet.org publication unless the publishing model changes and the security policy is updated.

Recommended order:

1. Confirm NuGet Trusted Publishing is configured for the current package identity.
2. Confirm the `template-package-publish` GitHub environment requires deliberate maintainer approval.
3. Run the `Publish Template Package` workflow manually with `skip_publish` enabled to confirm packing and artifact generation.
4. Review generated `.nupkg` metadata before publishing to NuGet.org.
5. Publish only after manual approval.
6. Confirm the published NuGet package page, README, package icon, package version, package ID, repository URL, license, authorship metadata, and tags render correctly.

## 5. Package Signing Policy Confirmation

Before publication, confirm one of the following is true:

- Package author signing remains intentionally deferred for this release.
- A project-controlled signing certificate, timestamping approach, and signing policy are configured and documented.

External contributors are not expected to sign release packages. Official packages should be produced only by the maintainer-controlled release workflow.

## 6. GitHub Release, Zenodo, and Documentation Validation

After creating the release candidate or stable release:

1. Confirm the GitHub Release points to the expected tag.
2. Confirm attached artifacts are present and versioned correctly.
3. Confirm Zenodo receives the intended release only after integration is enabled.
4. Confirm Zenodo metadata matches `CITATION.cff` and `.zenodo.json`.
5. Confirm the DocFX documentation workflow completes.
6. Confirm the published GitHub Pages documentation loads successfully.
7. Confirm release badges and documentation links in README resolve correctly.
8. Confirm installation instructions use the current package identity.
9. Confirm the production deployment checklist is current.
10. Confirm release notes link to the production deployment checklist, package page, documentation site, and DOI/archive record when available.
11. Confirm the merged release branch is absent from the remote branch list. If a legacy release branch remains, verify its immutable tag and published GitHub Release before deleting it.

## 7. Clean-Environment Post-Release Validation

After package publication, validate the published package from a clean environment rather than relying only on the repository workspace.

Recommended smoke test:

```powershell
mkdir C:\Temp\NetCoreApplicationTemplate-smoke
cd C:\Temp\NetCoreApplicationTemplate-smoke

dotnet new uninstall NetCoreApplicationTemplate
dotnet new install NetCoreApplicationTemplate

dotnet new netcoreapp-template -n SmokeTestApp
cd SmokeTestApp

dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

Then confirm:

- The generated solution builds cleanly.
- Tests pass.
- No maintainer-only files are scaffolded.
- README/package usage instructions match the published package behavior.
- The generated app can be started locally using documented instructions.

## 8. Visual Studio Template Validation

After package publication, validate that the template also works through Visual Studio's Create a new project experience.

Minimum Visual Studio smoke test:

1. Install the published package using `dotnet new install NetCoreApplicationTemplate`.
2. Restart Visual Studio.
3. Create a new project from `.NET Core Application Template`.
4. Validate at least the following option combinations:
   - `authProvider = cookie`, `dbProvider = sqlite`
   - `authProvider = cookie`, `dbProvider = sqlserver`
   - `authProvider = none`, `dbProvider = none`
5. Confirm the generated solution loads all expected projects.
6. Confirm restore and build succeed from Visual Studio.
7. Confirm the generated solution can also build from the command line.

For generated solutions, confirm project references resolve correctly and no generated project references a project that Visual Studio failed to load.

## 9. Container Smoke Validation

If containers are published for the release, validate them after publication:

- Confirm image tags match the release version.
- Confirm image digest/checksum information is available where applicable.
- Pull the published image from a clean environment.
- Start the container using documented configuration.
- Confirm the health endpoint or root smoke check responds as expected.
- Confirm logs do not expose secrets or release-only sensitive values.

Skip this section only when no container image is published for the release.

## 10. Rollback and Hotfix Guidance

Stable public artifacts should not be overwritten after publication.

Use this guidance for critical post-release issues:

- If the release fails before public publication, fix the workflow or release branch and rerun validation.
- If the GitHub Release is created but no downstream public artifact has been published, correct the release or recreate the tag only if it will not confuse consumers.
- If a NuGet package is published with incorrect metadata or behavior, publish a corrected patch version.
- If Zenodo metadata is incorrect before a DOI-bearing stable release, correct repository metadata before creating the stable release.
- If Zenodo has already archived the stable release, prefer a follow-up corrective release rather than rewriting history.
- If documentation is incorrect but package artifacts are valid, patch the documentation and note the correction in the next release notes when appropriate.
- If a security issue is discovered after release, create a hotfix branch from the release tag, apply the minimum safe fix, run the full release gate, and publish a patch release.

## 11. Zenodo Archival Sequence

Zenodo archives GitHub releases created after the GitHub integration is enabled.

Recommended order:

1. Confirm the GitHub repository is enabled in Zenodo.
2. Confirm `CITATION.cff` and `.zenodo.json` metadata are current.
3. Create a dry-run release tag when release metadata has changed materially.
4. Review the generated Zenodo DOI record and metadata.
5. Correct `CITATION.cff` or `.zenodo.json` if the DOI record is incomplete or misleading.
6. Create the stable release only after DOI metadata is acceptable.
7. Add or update the Zenodo DOI badge and citation block after the DOI/archive record is available.

## 12. Version Source of Truth

Before publishing a stable release, confirm every public version marker agrees with the intended release version.

| Surface | Source / location | Expected behavior |
|---|---|---|
| Assembly/package version | `Directory.Build.props` | `VersionPrefix`, `AssemblyVersion`, and `FileVersion` match the release version. |
| Template package metadata | `NetCoreApplicationTemplate.Template.csproj` | Package metadata resolves to the same version as the release tag. |
| Git tag | GitHub release tag | Stable releases use `vMAJOR.MINOR.PATCH`. |
| NuGet package | Published `.nupkg` metadata | Package version matches the Git tag without the leading `v`. |
| Container image | Published image tags | Version tag matches the Git tag without the leading `v`; digest is recorded. |
| Changelog | `CHANGELOG.md` | Latest heading matches the release version and date. |
| Citation metadata | `CITATION.cff` and `.zenodo.json` | Metadata reflects the stable release and DOI/archive expectations. |
| GitHub Release | Release title/body/assets | Release notes, attached assets, and links match the release tag. |
| Documentation | README and DocFX pages | Install commands, badges, release links, package identity, and citation guidance match the published release. |

## 13. Changelog and Release Note Process

Before creating a stable release:

1. Add a new `CHANGELOG.md` section above the previous release.
2. Use the heading format `## MAJOR.MINOR.PATCH - YYYY-MM-DD`.
3. Group entries under headings such as `Added`, `Changed`, `Fixed`, and `Security`.
4. Confirm the changelog includes user-facing package, container, documentation, metadata, and security-governance changes.
5. Use the changelog section as the starting point for GitHub Release notes.
6. Add links from the GitHub Release notes to the production deployment checklist, package page, documentation site, and DOI/archive record when available.
7. Do not describe unpublished artifacts as available until they have been validated after publication.

## 14. Maintainer Review Points

Review before NuGet publication:

- Package ID is correct.
- Version is correct.
- README/package README render correctly.
- Repository URL, license, authorship, tags, and description are correct.
- The `.nupkg` installs successfully in a clean environment.
- No maintainer-only files are included in scaffolded output.

Review before container publication:

- Image tags are correct.
- Image digest is captured.
- SBOM and provenance/attestation evidence are attached when produced.
- Vulnerability scan results are reviewed.
- Runtime smoke test passes.
- Logs do not expose secrets or release-only sensitive values.
