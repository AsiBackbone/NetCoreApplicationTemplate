# Repository Security Profile

This page defines the repository governance and publishing controls for the current `2.x` line and later while NetCoreApplicationTemplate remains solo-maintained.

It supersedes the earlier transitional profile that treated the first automated NuGet or container publication as a future trigger. Automated NuGet and GHCR publication now exist and are part of the normal operating model.

GitHub repository, branch-protection, security-analysis, and environment settings are not stored in git. Treat the values below as the required repository state and verify them in GitHub whenever permissions, publication workflows, maintainership, or release policy change.

## v2.x Control Matrix

| Control | Required v2.x state | Operating decision |
|:---|:---|:---|
| Secret scanning | Enabled | Detect committed provider-backed and supported secret patterns. |
| Secret-scanning push protection | Enabled | Block supported high-confidence secrets before they enter repository history. |
| Dependabot security updates | Enabled | Keep vulnerable dependency remediation active. |
| `main` pull-request flow | Required | Routine changes reach `main` through pull requests rather than direct pushes. |
| Required status checks | Enabled | CI/security checks required by branch protection must pass before merge. |
| Code Owner review | Enabled for owned-path changes | External or automation-authored changes require maintainer review. |
| Dismiss stale approvals | Enabled | New reviewable commits invalidate earlier approvals. |
| General required approval count | `0` while solo-maintained | A one-maintainer repository has no independent reviewer; requiring one would make normal maintainer PRs bypass-only. |
| Approval of most recent reviewable push | Disabled while solo-maintained | The control requires another authorized reviewer and becomes meaningful when independent review exists. |
| Required signed commits | Deferred | Enabling branch-level signature enforcement without a repository-wide signing policy can block unsigned commits introduced by maintainer, automation, or contributor branches. |
| Administrator bypass | Retained but constrained | Used only for the documented solo-maintainer self-authored PR path or an emergency; never as a routine direct-push workflow. |
| Force pushes / branch deletion on `main` | Disabled | Preserve protected history and the stable integration branch. |
| `release/*` branches | Short-lived preparation branches | They do not carry independent publishing authority; production tags come from the release commit merged into `main`. |
| `template-package-publish` environment | Deliberate maintainer approval required | Protect NuGet publication and Trusted Publishing OIDC use. |
| `container-publish` environment | Deliberate maintainer approval required | Protect GHCR publication, signing, provenance, and release-evidence writes. |

## Solo-Maintainer Review and Bypass Model

CODEOWNERS remains useful even with one maintainer because external or automation-authored pull requests that change owned paths must be reviewed by the maintainer before merge.

A sole Code Owner cannot provide independent approval for their own pull request. For maintainer-authored work, the normal path is therefore:

1. Open a pull request to `main`.
2. Allow the required CI and security checks to complete.
3. Review the final diff and release/security impact.
4. Use administrative bypass only when GitHub's self-review constraint would otherwise block the checked pull request.
5. Merge through GitHub so the pull request, checks, and review context remain part of the audit trail.

Administrative bypass is not a standing exception for routine direct pushes to `main`.

### Emergency Bypass

A direct administrative bypass is reserved for a situation where the normal pull-request path cannot safely restore repository operation, such as repairing a broken protection/workflow state or containing an active credential or publishing incident.

After an emergency bypass:

1. Open or update an issue describing why the bypass was necessary without exposing sensitive values.
2. Run the normal CI, security, version-consistency, and release checks against the resulting `main` state.
3. Reconcile any temporary branch, workflow, environment, or permission change back to the documented profile.
4. Rotate or revoke credentials when the event involved possible secret exposure.
5. Do not publish a stable package or container from the bypassed state until the release evidence is complete.

## Release Branch and Tag Boundary

Short-lived `release/*` branches are deliberately not a second trust boundary. They exist to stage version, changelog, documentation, and release-readiness changes.

The release sequence is:

```text
protected main
    -> short-lived release/* branch
    -> release pull request and validation
    -> merge back to protected main
    -> create vMAJOR.MINOR.PATCH tag on that merged main commit
    -> protected package/container publishing environments
```

If a release branch becomes long-lived, accepts independent changes, or is used as a direct publication source, protect it equivalently to `main` before using that model.

## Secret Pattern and Validity Policy

GitHub's supported provider-backed push-protection patterns are the baseline blocking control.

Do not enable broad generic or custom patterns solely to increase pattern count. For a repository-specific secret format that GitHub does not already cover:

1. Define the narrowest useful custom pattern.
2. Test it against representative repository content and known non-secret examples.
3. Review false positives before making it blocking.
4. Enable push protection for the custom pattern when GitHub supports it and the signal is reliable enough to block pushes.
5. Document any intentionally excluded pattern class when the exclusion has security significance.

Enable validity checks for supported provider patterns when available. Validity is a triage signal only. A credential found in repository history should be revoked or rotated even when GitHub reports it inactive, cannot validate it, or the provider no longer recognizes it.

Long-lived publishing credentials should not be introduced where federated or scoped workflow credentials already exist. NuGet.org uses Trusted Publishing through GitHub Actions OIDC. GHCR publication uses the workflow `GITHUB_TOKEN` with job-scoped permissions, and the published image is signed keylessly with provenance evidence.

## Publishing Compatibility

The v2.x profile is designed to preserve the existing release workflows rather than add protection that makes them bypass-only:

- `Publish Template Package` keeps `id-token: write` on the publish job for NuGet Trusted Publishing and uses the `template-package-publish` environment.
- `Publish Container` keeps write permissions isolated to the publish job, uses the `container-publish` environment, signs the pushed digest with cosign keyless signing, and emits provenance/attestation evidence.
- Release tags that trigger these workflows must point to the release commit already merged into `main`.
- Environment approval remains the human publication gate after the tag is created.

Required signed commits are intentionally separate from artifact signing. Deferring source-commit enforcement does not remove NuGet OIDC trust, container digest signing, environment approval, or provenance controls.

## Transition Beyond the Solo-Maintainer Profile

Re-evaluate this profile when any of the following occurs:

- A second maintainer receives write, maintain, or administrative repository access.
- Independent pull-request review becomes routinely available.
- A separate release or security approver is established.
- Publication authority expands to additional package owners, registries, environments, or organizations.
- Organizational or consumer policy requires signed source commits or two-person approval.
- A credential, publishing, or bypass incident shows the current model is insufficient.

At that transition, explicitly review:

- Raising the general required approval count to at least one.
- Enabling approval of the most recent reviewable push where appropriate.
- Removing or narrowing administrator bypass.
- Enabling required signed commits after maintainer, bot, and contribution signing paths are proven.
- Moving repeated branch-protection decisions into organization/repository rulesets when that improves consistency.
- Expanding CODEOWNERS to the new maintainer or review groups.

## Verification Checklist

Verify this non-versioned repository state before each stable release and after material governance changes:

```text
[ ] Secret scanning is enabled.
[ ] Secret-scanning push protection is enabled.
[ ] Dependabot security updates are enabled.
[ ] main requires pull-request flow and the intended required status checks.
[ ] Code Owner review and stale-approval behavior match the solo-maintainer model.
[ ] General approval count remains 0 until an independent reviewer exists.
[ ] Most-recent-push approval remains disabled until an independent reviewer exists.
[ ] Signed-commit enforcement remains intentionally deferred or has a documented replacement policy.
[ ] Administrator bypass has not become a routine direct-push path.
[ ] template-package-publish requires deliberate maintainer approval.
[ ] container-publish requires deliberate maintainer approval.
[ ] NuGet Trusted Publishing identity still matches this repository/workflow/environment.
[ ] Production release tags point to the release commit already merged into main.
```

## Related Documentation

- [Contributing](../../CONTRIBUTING.md)
- [Security Policy](../../SECURITY.md)
- [Maintainers](../../MAINTAINERS.md)
- [Release Checklist and Runbook](../../RELEASE.md)
- [GitHub Workflow](github-workflow.md)
- [Container Release Publishing](container-publish.md)
