# ADR-0005: Defer NuGet package signing with a mandatory review date

## Status

Accepted on 2026-09-12. Review no later than 2027-03-31 or before the first 3.0.0 release candidate, whichever occurs first.

Decision owner: [@cdcavell](https://github.com/cdcavell), repository and package publishing owner.

## Context

NetCoreApplicationTemplate publishes a NuGet template package and an OCI image. The OCI image is signed by digest with Cosign keyless signing, but that signature does not cover the NuGet package. NuGet Trusted Publishing authenticates the protected GitHub Actions workflow to NuGet.org; it is publisher authentication and is not an author signature embedded in the `.nupkg`.

The repository currently has one maintainer. Introducing NuGet author signing requires a project-controlled code-signing certificate, secure certificate custody, timestamping, rotation and revocation procedures, NuGet.org certificate registration, and a recovery path that does not depend on one person's workstation. Those operational controls are not yet in place.

SBOMs, Source Link metadata, release tags, hashes, and GitHub artifact attestations improve traceability but are separate trust signals. None is represented as a substitute for NuGet package signing.

## Decision

Defer NuGet author signing for the `NetCoreApplicationTemplate` package until the review deadline or an earlier trigger below. Continue publishing only through the protected `template-package-publish` environment and NuGet Trusted Publishing.

For every new tagged release, publish durable evidence for the exact `.nupkg`: an SPDX SBOM, SHA-256 hash, source tag and commit, and GitHub build-provenance attestations. Attach the evidence to the GitHub Release and record it in `release-evidence-manifest.json`.

Keep the following controls explicitly separate:

- Cosign signs only the published OCI image digest.
- NuGet Trusted Publishing authorizes the workflow to publish the package.
- GitHub artifact attestations bind workflow provenance to the package and evidence files.
- SBOMs, Source Link, release tags, and retained SHA-256 hashes support inspection and correlation.
- NuGet package author signing remains absent until this decision is replaced.

Re-evaluate this decision immediately if a consumer or registry requires signed packages, package ownership or maintainership expands, publication becomes fully automated, a relevant supply-chain incident occurs, or a practical project-controlled certificate service becomes available.

Adopting package signing requires a replacement ADR that identifies the certificate owner and storage boundary, timestamp authority, NuGet.org registration, workflow integration, rotation schedule, revocation and incident response, verification commands, and recovery ownership.

## Consequences

Consumers can durably retrieve package identity, composition, hash, source commit, and provenance evidence from the corresponding GitHub Release. They must nevertheless accept the residual risk that the `.nupkg` has no NuGet author signature.

The review is time-bounded and owner-assigned instead of being an open-ended deferral. Release reviews must not describe Cosign, Trusted Publishing, provenance, or an SBOM as package signing.

The release workflows become coupled at the evidence-publication boundary: both protected publish jobs must finish so the unified release manifest can be assembled and verified.

## Alternatives Considered

- Sign immediately with a maintainer-controlled certificate. Rejected because durable certificate custody, rotation, revocation, timestamping, and recovery controls are not yet established.
- Treat NuGet Trusted Publishing as package signing. Rejected because it authenticates publication but does not place an author signature in the package.
- Rely only on temporary GitHub Actions artifacts. Rejected because their retention expires while published packages remain available.

## Related References

- [Issue #506](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/506)
- [Security Policy](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/SECURITY.md)
- [Release Checklist and Runbook](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/RELEASE.md)
- [Container Release Publishing](../articles/container-publish.md)
