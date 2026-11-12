# Container Release Publishing

This article documents the tag-driven container release workflow for the repository-maintained template image.

The published image is intended as release evidence for the template repository. It is not required for local template development, and generated consumer applications may choose their own registry, image name, and deployment process.

## Published Image

The release workflow publishes the repository image to GitHub Container Registry:

```text
ghcr.io/asibackbone/netcoreapplicationtemplate
```

A tag push matching `v*.*.*` starts the `Publish Stable Release` workflow, which runs the container release workflow after the template package job succeeds.

Stable semantic version tags publish:

```text
ghcr.io/asibackbone/netcoreapplicationtemplate:<major>.<minor>.<patch>
ghcr.io/asibackbone/netcoreapplicationtemplate:<major>
ghcr.io/asibackbone/netcoreapplicationtemplate:latest
```

Prerelease tags publish the full version tag only and do not update `latest` or the major tag.

## Release Safety Gate

Container publication has no GitHub environment of its own. `Publish Stable Release` calls it only after the template package job succeeds, and that job targets the `template-package-publish` environment. The single maintainer approval on that environment therefore gates both NuGet and GHCR publication for a release tag.

The `template-package-publish` environment is an ongoing publication boundary, not a one-time bootstrap step. Keep deliberate maintainer approval enabled so every production release has a manual approval gate:

- Environment name: `template-package-publish`
- Required reviewers: repository owner or maintainer
- Deployment branches and tags: allow release tags created from the protected `main` release commit

The [repository-control drift audit](repository-security-profile.md#drift-audit) checks that this environment exists and still requires reviewers.

## Build and Scan Flow

The workflow builds the Docker image from the repository Dockerfile, exports it to a tar archive, scans that archive, generates release evidence, and only then publishes.

Scanning runs through `aquasecurity/trivy-action`, pinned by commit SHA like every other action in this repository. Two properties follow from that. The scanner reads the exported archive rather than querying the Docker daemon, so no step mounts `/var/run/docker.sock` into a scanner container. And the Trivy version comes from the pinned action rather than a mutable image tag, so Dependabot's `github-actions` ecosystem proposes scanner updates alongside every other action.

The two tag-driven publish workflows cooperate to produce one durable release evidence set. The template-package workflow generates the SPDX SBOM and provenance for the exact `.nupkg`, signs that `.nupkg` with a Cosign keyless blob signature, attaches the package, its Sigstore bundle, and its evidence to the GitHub Release, verifies the anonymously downloaded package against the bundle, and only then publishes to NuGet.org. The container workflow publishes and signs the OCI digest, verifies the digest signature and provenance, downloads the package, bundle, and package evidence from the GitHub Release, verifies them again, assembles the unified manifest, attaches the complete set, and fails if any required asset is absent.

Review and approve the `template-package-publish` deployment for the release tag. Container publication starts automatically after the approved package job succeeds. If the package job fails or is rejected, the container job does not run; if the container job fails after the package was published, rerun the failed job after confirming the expected artifacts rather than publishing a new tag.

Release evidence includes:

- Trivy vulnerability scan output.
- Trivy SARIF results uploaded to GitHub code scanning.
- Separate SPDX JSON software bills of materials for the exact NuGet package and OCI image.
- Build provenance attestations for the exact `.nupkg`, package evidence JSON, and OCI image digest.
- A Cosign keyless blob signature for the exact `.nupkg`, published as a `*.sigstore.json` bundle beside the package.
- Cosign keyless signature for the published image digest.
- SHA-256 hashes, source tag and commit, OCI digest, signer identities, and tested verification commands in a unified manifest.

## Durable Release Assets

Every completed tagged release must expose these anonymously downloadable assets on its GitHub Release:

```text
NetCoreApplicationTemplate.<version>.nupkg
NetCoreApplicationTemplate.<version>.nupkg.sigstore.json
NetCoreApplicationTemplate.<version>.spdx.json
container-image-manifest.json
netcoreapplicationtemplate-<version>-release-notes.md
netcoreapplicationtemplate-container-<version>.spdx.json
release-evidence-manifest.json
template-package-manifest.json
trivy-results.sarif
```

Releases up to and including `v2.11.9` predate package signing and do not include the `.nupkg` or `.nupkg.sigstore.json` assets; their seven evidence assets are unchanged.

`release-evidence-manifest.json` binds the release tag and source commit to the NuGet package SHA-256, the package Sigstore bundle and its signer identity, both SBOMs, the OCI digest, and every asset hash. `container-image-manifest.json` records the digest-specific container signer identity and verification commands. The release jobs verify the complete asset list after upload and fail closed if an asset cannot be retrieved from the public release.

## Release Signatures

Two separate Cosign keyless signatures are produced. Each binds one subject to one protected workflow identity through a short-lived Fulcio certificate issued from the job's GitHub Actions OIDC token and a Rekor transparency-log entry. No long-lived signing key or secret is used.

| Subject | Signed by | Certificate identity | Verified by |
| --- | --- | --- | --- |
| Exact `.nupkg` attached to the GitHub Release | `publish-template` job in the protected `template-package-publish` environment | `https://github.com/AsiBackbone/NetCoreApplicationTemplate/.github/workflows/publish-template-package.yml@refs/tags/v<version>` | `scripts/Assert-SigstorePackageBundle.ps1` before NuGet publication and again in the container workflow |
| Published OCI image digest | container `publish` job | `https://github.com/AsiBackbone/NetCoreApplicationTemplate/.github/workflows/publish-container.yml@refs/tags/v<version>` | `scripts/Assert-SigstoreContainerSignature.ps1` immediately after signing |

Both reusable workflows run from `Publish Stable Release`, so the certificate identity names the reusable workflow file at the release tag. Every check also pins the issuer `https://token.actions.githubusercontent.com`, the repository `AsiBackbone/NetCoreApplicationTemplate`, the ref `refs/tags/v<version>`, the release commit, and the `push` trigger. A tag publication started any other way, such as a manual dispatch, produces a different trigger and fails the gate before NuGet publication.

The release gates fail when the package or bundle is missing, the package digest differs from `template-package-manifest.json`, the bundle is malformed, or the signature does not verify for the exact identity. They also require that a modified package and untrusted workflow identities are rejected, so a verifier that accepts everything cannot pass. `scripts/Test-SigstoreVerification.ps1` exercises these rules in CI.

Boundaries:

- The package bundle covers only the `.nupkg` bytes attached to the GitHub Release. NuGet.org repository-signs packages on ingestion, so the NuGet.org copy has a different digest and is not covered by the bundle. Verify that copy with `dotnet nuget verify --all <package-path>`.
- The image signature covers only the OCI digest. It does not cover the `.nupkg`, and the package bundle does not cover the image.
- Neither is a NuGet author signature; NuGet author signing remains deferred under [ADR-0005](../adr/0005-defer-nuget-package-signing.md). NuGet Trusted Publishing authenticates publication and is separate again.
- These are release and supply-chain controls. They add no Sigstore runtime component to generated applications and do not change generated authentication or authorization.
- OpenSSF Scorecard may detect the `*.sigstore.json` release asset in its Signed-Releases check. Review Scorecard after publication as a discoverability signal only; it is not proof that a signature is valid and not a release acceptance condition.

GitHub Actions artifacts are temporary workflow hand-offs and diagnostic copies. They are not the durable consumer distribution point and may expire while the NuGet package and container remain published. Consumers should retrieve long-lived evidence from the GitHub Release.

Critical and High vulnerabilities fail the build unless the repository explicitly documents an accepted-risk exception in a future change.

## Permissions Model

The workflow keeps default permissions read-only and grants write permissions only in the jobs that need them.

The publish job requires:

- `packages: write` to push the image to GHCR.
- `contents: write` to create or update the GitHub Release and attach evidence.
- `id-token: write` to support keyless signing and provenance attestation.
- `attestations: write` to publish artifact provenance metadata.
- `security-events: write` to upload SARIF vulnerability results.

## Health Probe Contract

The container image listens on port `8080`.

Health probe paths are:

```text
/health/live
/health/ready
```

The Dockerfile intentionally delegates active HTTP health probing to Docker Compose, Kubernetes, load balancers, or hosting infrastructure rather than adding probe-only tools such as curl or wget to the runtime image.

## Local Verification

Before tagging a release, build the local image:

```powershell
docker build -t projecttemplate-web:dev .
```

Run the container:

```powershell
docker run --rm -p 127.0.0.1:8080:8080 projecttemplate-web:dev
```

Verify the probe contract:

```powershell
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
```

## Consumer Evidence Verification

Download the release assets and inspect `release-evidence-manifest.json` first. From the directory containing the assets, verify the recorded component hashes:

```powershell
$manifest = Get-Content ./release-evidence-manifest.json -Raw | ConvertFrom-Json
foreach ($asset in $manifest.assets) {
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $asset.name).Hash.ToLowerInvariant()
    if ($actual -ne $asset.sha256) { throw "Hash mismatch: $($asset.name)" }
}
```

The commands below download from the public release URLs and need no GitHub credentials, so they keep working after workflow artifacts expire. Verify the release package against the package manifest, then inspect its SPDX document:

```powershell
$tag = 'v<version>'
$base = "https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/download/$tag"
Invoke-WebRequest "$base/template-package-manifest.json" -OutFile ./template-package-manifest.json
$package = Get-Content ./template-package-manifest.json -Raw | ConvertFrom-Json
$version = $package.package.version
$packagePath = "./NetCoreApplicationTemplate.$version.nupkg"
Invoke-WebRequest "$base/NetCoreApplicationTemplate.$version.nupkg" -OutFile $packagePath
Invoke-WebRequest "$base/NetCoreApplicationTemplate.$version.nupkg.sigstore.json" -OutFile "$packagePath.sigstore.json"
$actualPackageHash = (Get-FileHash -Algorithm SHA256 $packagePath).Hash.ToLowerInvariant()
if ($actualPackageHash -ne $package.package.packageSha256) { throw 'Template package hash mismatch.' }
Get-Content ./NetCoreApplicationTemplate.$version.spdx.json -Raw | ConvertFrom-Json | Select-Object name,documentNamespace
```

Verify the package's Sigstore bundle against the exact signer identity. Take the commit from `release-evidence-manifest.json` and confirm it is the commit the tag points to, for example with `git rev-parse "$tag^{commit}"` in a clone:

```powershell
$sourceCommit = (Get-Content ./release-evidence-manifest.json -Raw | ConvertFrom-Json).sourceCommit
cosign verify-blob $packagePath `
  --bundle "$packagePath.sigstore.json" `
  --certificate-identity "https://github.com/AsiBackbone/NetCoreApplicationTemplate/.github/workflows/publish-template-package.yml@refs/tags/$tag" `
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" `
  --certificate-github-workflow-repository AsiBackbone/NetCoreApplicationTemplate `
  --certificate-github-workflow-ref "refs/tags/$tag" `
  --certificate-github-workflow-sha $sourceCommit `
  --certificate-github-workflow-trigger push
```

A successful check prints `Verified OK`. To confirm your verifier rejects tampering, run the same command against a modified copy (for example after `Add-Content` of one byte) and expect a non-zero exit code.

A `.nupkg` downloaded from NuGet.org is repository-signed by NuGet.org and has a different digest, so it matches neither `packageSha256` nor the bundle. Verify that copy with `dotnet nuget verify --all <package-path>`. Releases up to and including `v2.11.9` do not attach the `.nupkg` to the GitHub Release and have no package bundle.

For releases produced after this evidence workflow was introduced, verify GitHub provenance for the downloaded package:

```powershell
gh attestation verify ./NetCoreApplicationTemplate.<version>.nupkg --repo AsiBackbone/NetCoreApplicationTemplate
```

Verify the OCI signature and provenance using the immutable digest from `container-image-manifest.json`:

Authenticate to GHCR first when the package visibility or local GitHub token requires it; `gh attestation verify` needs registry pull access in addition to repository access.

```powershell
cosign verify `
  --certificate-identity "https://github.com/AsiBackbone/NetCoreApplicationTemplate/.github/workflows/publish-container.yml@refs/tags/v<version>" `
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" `
  --certificate-github-workflow-repository AsiBackbone/NetCoreApplicationTemplate `
  --certificate-github-workflow-ref "refs/tags/v<version>" `
  --certificate-github-workflow-trigger push `
  ghcr.io/asibackbone/netcoreapplicationtemplate@sha256:<digest>

gh attestation verify `
  oci://ghcr.io/asibackbone/netcoreapplicationtemplate@sha256:<digest> `
  --repo AsiBackbone/NetCoreApplicationTemplate
```

Always verify by `@sha256:<digest>`. A mutable tag such as `:latest` or `:<major>` can be moved and is not the signed subject.

### Trust roots and online/offline verification

- Use Cosign 3.x or later. It reads the Sigstore protobuf bundle format used by both signatures.
- Cosign validates signatures against the Sigstore public-good trusted root (Fulcio certificate authority, Rekor transparency log, and timestamp keys). It retrieves that trusted root through TUF from `tuf-repo-cdn.sigstore.dev` and caches it locally.
- The package bundle already contains the certificate, signature, and transparency-log inclusion proof, so `cosign verify-blob` needs no Fulcio or Rekor call and works offline when you pass a separately obtained `trusted_root.json` with `--trusted-root <path>`.
- Image verification must reach GHCR to read the signature attached to the digest; it cannot run fully offline.
- Verification fails, and the artifact must be rejected, when the bytes or digest differ from the signed subject, the identity, issuer, repository, ref, commit, or trigger differs, the bundle or signature is missing or malformed, or the trusted root cannot be obtained or does not validate the signature.

Release `v2.9.0` was backfilled from its retained original container evidence and the exact package downloaded from NuGet.org. Its manifest identifies which evidence was regenerated after publication; the retained package-build artifact differed from the immutable public package, so no retroactive package provenance claim is made.

## Release Checklist

```text
[ ] Confirm the release tag follows semantic versioning and resolves to the release commit already merged into `main`.
[ ] Confirm the `template-package-publish` environment still requires deliberate maintainer approval, which also gates container publication.
[ ] Confirm CI and template smoke tests are passing.
[ ] Confirm Docker build succeeds locally or in CI.
[ ] Confirm Trivy scan has no unaccepted Critical or High findings.
[ ] Confirm all nine durable assets listed above are attached to the release.
[ ] Confirm release-evidence-manifest.json hashes match every component asset.
[ ] Confirm the .nupkg Sigstore bundle verifies for the publish-template-package.yml release-tag identity.
[ ] Confirm the image digest signature verifies for the publish-container.yml release-tag identity.
[ ] Confirm provenance attestation is present.
[ ] Confirm package signing is described according to ADR-0005 and is not conflated with OCI signing, the package Sigstore bundle, or Trusted Publishing.
[ ] After publication, review the OpenSSF Scorecard Signed-Releases result as a discoverability signal only.
[ ] Confirm GHCR tags match the release version policy.
```
