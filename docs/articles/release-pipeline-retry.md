# Stable Release Pipeline and Retry Behavior

Stable `v*.*.*` releases use one ordered publication pipeline. The tag triggers `Publish Stable Release`; the reusable template-package workflow completes its evidence boundary and NuGet publication before the reusable container workflow can begin.

## Ordered publication sequence

The production path is:

1. Build and validate the template package.
2. Generate the package SPDX SBOM and package manifest.
3. Attest the package and package evidence when the run is tag-based.
4. Sign the exact package with Cosign keyless signing and verify the bundle against the exact signer identity, including tamper and untrusted-identity cases.
5. Create or resolve the GitHub Release.
6. Upload the package, its Sigstore bundle, and the package evidence to the GitHub Release.
7. Verify that those assets are present and anonymously retrievable, and verify the anonymously downloaded package against its bundle.
8. Push the package to the configured NuGet source.
9. Build, scan, and package the container image.
10. Publish the versioned container image and resolve its immutable digest.
11. Sign the image digest, publish container provenance, and verify both against the exact signer identity.
12. Download the already-published package, bundle, and package evidence without polling, and verify the package signature and provenance again.
13. Generate and upload the unified release-evidence manifest.
14. Verify the complete GitHub Release evidence set.

The `template-package-publish` environment remains the deliberate maintainer approval boundary. The container workflow is reusable and is reached only through the orchestrator's explicit `needs:` dependency after the package stage succeeds. This removes the former mutual 30-minute polling dependency between the package and container workflows.

## Evidence-first NuGet boundary

NuGet publication is intentionally placed after all retryable package preparation and release-evidence checks. A tagged run cannot reach `dotnet nuget push` until the package SBOM and manifest have been uploaded to the GitHub Release and `Assert-GitHubReleaseAssets.ps1` has verified that those assets are anonymously retrievable.

The NuGet push retains `--skip-duplicate` so rerunning the same release is safe when the provider reports that the exact package version already exists. A published stable package version is never overwritten.

## Failure and retry matrix

| Failure point | Public state | Retry guidance |
| --- | --- | --- |
| Package build, validation, SBOM generation, or provenance attestation | No NuGet publication | Correct the failure and rerun. No registry cleanup is required. |
| Package signing or Sigstore bundle verification | No NuGet publication | Correct the failure and rerun the same tag workflow. A rerun signs the same package again and replaces the bundle. A run not started by the tag push fails the trigger check by design. |
| GitHub Release creation, package-evidence upload, or anonymous evidence verification | No NuGet publication | Correct the release/evidence problem and rerun. Evidence upload uses `--clobber`. |
| NuGet push | Package may or may not exist | Check the package source, then rerun the same tag workflow. `--skip-duplicate` makes an already-published version non-fatal. |
| Container build or vulnerability threshold after NuGet publication | NuGet is public; container is not | Correct the container-only failure and rerun the same tag workflow. The package stage is duplicate-safe. |
| GHCR push, signing, container provenance, or image signature verification | NuGet is public; image may be partially published | Inspect the version digest and tags. Rerun only when the same release commit and intended image are being published; otherwise use a corrective patch release. |
| Unified evidence generation, upload, or final verification | NuGet and container are public | Rerun immediately to restore the durable evidence boundary. Evidence upload uses `--clobber`; do not change already-published artifact identity. |

## Missing evidence behavior

The container stage no longer waits for package evidence. Once the package job has succeeded, the expected package SBOM and manifest must already be present on the GitHub Release. If either asset is missing, download or verification fails immediately and the release run stops.

This fail-fast behavior is intentional: ordering is represented by workflow dependencies rather than by timing assumptions or maintainer approval timing.

## Production entry point

For a production stable release, create the `vMAJOR.MINOR.PATCH` tag only after the release commit is merged into protected `main`. Do not use the reusable container workflow as an independent production release entry point. Manual template-package runs remain useful for pack-only validation through `skip_publish`.
