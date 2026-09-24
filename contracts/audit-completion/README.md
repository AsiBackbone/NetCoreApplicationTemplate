# Audit-Completion Contract Vectors

This directory publishes machine-readable vectors for NCAT's audit-completion handoff. External adapters, such as AsiBackbone's `samples/NcatAuditCompletionAdapter`, can use them to confirm that their independently modeled receipt, message, idempotency, and canonical manifest behavior still matches NCAT, without taking NCAT as a compile-time dependency.

The directory is repository-only. It is not packed into the `dotnet new` template and does not appear in generated applications.

## Layout

| Path | Contents |
| --- | --- |
| `v1/audit-completion-vectors.json` | Contract version `1.x` vectors: valid committed vectors, outcomes that produce no message, and invalid messages an adapter must reject. |

The directory name carries the contract's major version. A breaking change adds a new `vN/` directory; earlier directories stay in place, unchanged, for consumers that have not migrated.

## How the vectors are produced

The fixture is generated, not handwritten. [`AuditCompletionContractVectorTests`](../../tests/ProjectTemplate.Web.Tests/AuditCompletionContractVectorTests.cs) builds every value through production NCAT code:

- `CanonicalApplicationMutationManifestBuilder` produces the canonical manifest text;
- `Sha256ApplicationMutationManifestHasher` produces the digest;
- `ApplicationMutationManifestVerifier` confirms each receipt against its records;
- `ApplicationAuditCompletionOutbox` stages each receipt in SQLite and dispatches it, which produces the destination, idempotency key, and message;
- the verifier and outbox staging confirm that NCAT itself rejects the invalid messages marked `ncat-manifest-verifier` or `ncat-outbox-staging`.

The test then compares its output byte for byte with the committed file. Any change to canonicalization, hashing, idempotency derivation, message mapping, or JSON field names fails the build until the fixture is regenerated on purpose.

A second test runs real `SaveChanges` calls to confirm that `Committed` is the only persistence outcome NCAT emits. A save with no changes produces no receipt. A failed or rolled-back audited transaction leaves no outbox entry.

All values are synthetic. The vectors contain no personal data, credentials, or environment-specific values.

## Fixture structure

| Field | Meaning |
| --- | --- |
| `contract`, `contractVersion` | Contract identifier and `MAJOR.MINOR` version. |
| `generation` | Source repository, generating test, and regeneration command. |
| `schemaVersions` | Completion message, canonical manifest, and audit record schema versions. |
| `manifest` | Digest algorithm (`SHA-256`), digest encoding (uppercase hex), and byte encoding (UTF-8, no BOM). |
| `idempotency` | Key prefix and derivation: `ncat-audit-completion:` + uppercase hex SHA-256 of `<destination>\n<mutationBatchId>`. |
| `persistenceOutcomes` | Outcomes that produce messages (`supported`) and scenarios that produce none (`withoutReceipt`). |
| `vectors[]` | Valid cases: the retained `auditRecords` input, the `canonicalManifest` text with its UTF-8 byte length and expected digest, the `receipt`, and the dispatched `message`. |
| `invalidMessages[]` | Messages an adapter must reject, each with a `rule` and `enforcedBy` (`ncat-manifest-verifier`, `ncat-outbox-staging`, or `adapter`). |

`canonicalManifest.json` is the exact text that is hashed. Encode it as UTF-8 without a BOM, hash it with SHA-256, and compare the result with `expectedDigest`. The escape sequences inside that string, such as `é` and `+`, are part of the canonical form. They are what the production JSON writer emits, and an adapter that recanonicalizes must reproduce them.

`receipt` and `message` use camelCase property names, the same shape as `System.Text.Json` web defaults. NCAT does not prescribe a wire format beyond this: the adapter owns the protocol, and these objects describe the fields and values it receives.

## Consuming the vectors

Fetch the file at a pinned revision instead of `main`:

```text
https://raw.githubusercontent.com/AsiBackbone/NetCoreApplicationTemplate/<tag-or-commit>/contracts/audit-completion/v1/audit-completion-vectors.json
```

Pin a release tag that contains `contracts/audit-completion/v1/`. The first release to contain it is the one after `v2.10.0`. Until then, pin a commit SHA from `main`. Vendor the pinned copy into the consuming repository's test assets and update it deliberately, so that an NCAT change shows up as a reviewed diff rather than an unexpected test failure.

A consuming adapter's tests should:

1. for each entry in `vectors`, rebuild the canonical manifest from `auditRecords` (if the adapter canonicalizes), check its digest against `expectedDigest`, and confirm that the adapter accepts `message`;
2. recompute the idempotency key from `message.destination` and `message.mutationBatchId`, and compare it with `message.idempotencyKey`;
3. for each entry in `invalidMessages`, confirm that the adapter rejects `message`;
4. reject any `contractVersion` whose major version it does not support.

## Compatibility policy

The contract version is `MAJOR.MINOR`. The file and its directory change only through a pull request that regenerates the fixture with the contract test.

| Change | Classification | Required action |
| --- | --- | --- |
| New optional field on the receipt, message, or fixture; new valid or invalid vector; new documentation field | Additive | Increment `MINOR`. Adapters must ignore fields they do not recognize. |
| Removed or renamed field; changed field type or nullability; a previously optional field becoming required | Breaking | Increment `MAJOR` and publish a new `vN/` directory. Leave the previous directory unchanged. |
| Change to `schemaVersions.completionMessage` | Breaking | Increment `MAJOR`. The new message schema version must also change in `ApplicationAuditCompletionOutboxEntry.CurrentSchemaVersion`. |
| Change to canonicalization rules, manifest schema version, digest algorithm, or digest encoding (any change to `expectedDigest` for existing input) | Breaking | Increment `ApplicationMutationManifest.CurrentSchemaVersion` and the contract `MAJOR`. Previously issued receipts keep verifying against the schema version recorded in them. |
| Change to idempotency key derivation or prefix | Breaking | Increment `MAJOR`. Existing outbox rows keep their stored keys. Document the effect on in-flight deliveries. |
| New supported persistence outcome | Breaking | Increment `MAJOR`. Adapters must not guess the meaning of unknown outcomes. |

Every breaking change needs a `CHANGELOG.md` entry marked **Behavior change** that links the new vector directory. It also needs a companion issue for AsiBackbone's adapter.

## Regenerating the fixture

After an intentional contract change, and after applying the policy above:

```bash
NCAT_UPDATE_CONTRACT_VECTORS=true dotnet test --configuration Release
```

Review the resulting diff to `audit-completion-vectors.json` before you commit it. The test never regenerates the fixture in CI.
