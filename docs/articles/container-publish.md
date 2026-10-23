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

The two tag-driven publish workflows cooperate to produce one durable release evidence set. The template-package workflow publishes the exact `.nupkg`, generates its SPDX SBOM and provenance, and attaches its evidence. The container workflow publishes and signs the OCI digest, downloads the package evidence from the GitHub Release, assembles the unified manifest, attaches the complete set, and fails if any required asset is absent.

Review and approve the `template-package-publish` deployment for the release tag. Container publication starts automatically after the approved package job succeeds. If the package job fails or is rejected, the container job does not run; if the container job fails after the package was published, rerun the failed job after confirming the expected artifacts rather than publishing a new tag.

Release evidence includes:

- Trivy vulnerability scan output.
- Trivy SARIF results uploaded to GitHub code scanning.
- Separate SPDX JSON software bills of materials for the exact NuGet package and OCI image.
- Build provenance attestations for the exact `.nupkg`, package evidence JSON, and OCI image digest.
- Cosign keyless signature for the published image digest.
- SHA-256 hashes, source tag and commit, OCI digest, and tested verification commands in a unified manifest.

## Durable Release Assets

Every completed tagged release must expose these anonymously downloadable assets on its GitHub Release:

```text
NetCoreApplicationTemplate.<version>.spdx.json
container-image-manifest.json
netcoreapplicationtemplate-<version>-release-notes.md
netcoreapplicationtemplate-container-<version>.spdx.json
release-evidence-manifest.json
template-package-manifest.json
trivy-results.sarif
```

`release-evidence-manifest.json` binds the release tag and source commit to the NuGet package SHA-256, both SBOMs, the OCI digest, and every component asset hash. `container-image-manifest.json` records the digest-specific container verification commands. The release job verifies the complete asset list after upload and fails closed if an asset cannot be retrieved from the public release.

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

Verify the downloaded NuGet package against the package manifest, then inspect its SPDX document:

```powershell
$package = Get-Content ./template-package-manifest.json -Raw | ConvertFrom-Json
$version = $package.package.version
$packagePath = "./NetCoreApplicationTemplate.$version.nupkg"
Invoke-WebRequest `
  -Uri "https://api.nuget.org/v3-flatcontainer/netcoreapplicationtemplate/$version/netcoreapplicationtemplate.$version.nupkg" `
  -OutFile $packagePath
$actualPackageHash = (Get-FileHash -Algorithm SHA256 $packagePath).Hash.ToLowerInvariant()
if ($actualPackageHash -ne $package.package.packageSha256) { throw 'NuGet package hash mismatch.' }
Get-Content ./NetCoreApplicationTemplate.$($package.package.version).spdx.json -Raw | ConvertFrom-Json | Select-Object name,documentNamespace
```

The `.nupkg` is published on NuGet.org rather than duplicated as a GitHub Release asset.

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
  ghcr.io/asibackbone/netcoreapplicationtemplate@sha256:<digest>

gh attestation verify `
  oci://ghcr.io/asibackbone/netcoreapplicationtemplate@sha256:<digest> `
  --repo AsiBackbone/NetCoreApplicationTemplate
```

Release `v2.9.0` was backfilled from its retained original container evidence and the exact package downloaded from NuGet.org. Its manifest identifies which evidence was regenerated after publication; the retained package-build artifact differed from the immutable public package, so no retroactive package provenance claim is made.

## Release Checklist

```text
[ ] Confirm the release tag follows semantic versioning and resolves to the release commit already merged into `main`.
[ ] Confirm the `template-package-publish` environment still requires deliberate maintainer approval, which also gates container publication.
[ ] Confirm CI and template smoke tests are passing.
[ ] Confirm Docker build succeeds locally or in CI.
[ ] Confirm Trivy scan has no unaccepted Critical or High findings.
[ ] Confirm all seven durable assets listed above are attached to the release.
[ ] Confirm release-evidence-manifest.json hashes match every component asset.
[ ] Confirm the image digest is signed.
[ ] Confirm provenance attestation is present.
[ ] Confirm package signing is described according to ADR-0005 and is not conflated with OCI signing or Trusted Publishing.
[ ] Confirm GHCR tags match the release version policy.
```
