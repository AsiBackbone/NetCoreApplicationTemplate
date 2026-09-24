using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Regenerates the published audit-completion contract vectors through production NCAT builders, hashers,
/// outbox staging, and dispatch, then requires the committed fixture to match byte for byte.
/// </summary>
/// <remarks>
/// Set <c>NCAT_UPDATE_CONTRACT_VECTORS=true</c> to rewrite the fixture after an intentional contract change.
/// Review the compatibility policy in <c>contracts/audit-completion/README.md</c> before committing the result.
/// </remarks>
public sealed class AuditCompletionContractVectorTests
{
    private const string _contractVersion = "1.0";
    private const string _updateEnvironmentVariable = "NCAT_UPDATE_CONTRACT_VECTORS";
    private const string _defaultDestination = "default";
    private const string _archiveDestination = "accountability-archive";

    private static readonly DateTimeOffset _completedUtc = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _stagedUtc = new(2026, 7, 18, 12, 0, 5, TimeSpan.Zero);

    private static readonly JsonSerializerOptions _contractJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions _fixtureWriterOptions = new()
    {
        // The fixture is data for other repositories, not HTML; keep it readable. The canonical manifest text
        // inside it retains the production escaping because it is stored as a string value.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    [Fact]
    public async Task ContractVectors_MatchProductionBehavior()
    {
        JsonObject document = await BuildContractDocumentAsync();
        string expected = document.ToJsonString(_fixtureWriterOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        string fixturePath = GetFixturePath();

        if (string.Equals(Environment.GetEnvironmentVariable(_updateEnvironmentVariable), "true", StringComparison.OrdinalIgnoreCase))
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
            await File.WriteAllTextAsync(fixturePath, expected, new UTF8Encoding(false), TestContext.Current.CancellationToken);
        }

        Assert.True(
            File.Exists(fixturePath),
            $"Contract fixture '{fixturePath}' is missing. Set {_updateEnvironmentVariable}=true to generate it.");
        string actual = (await File.ReadAllTextAsync(fixturePath, TestContext.Current.CancellationToken))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.True(
            string.Equals(expected, actual, StringComparison.Ordinal),
            $"Production audit-completion behavior no longer matches '{fixturePath}'. " +
            "If the change is intentional, follow the compatibility policy in contracts/audit-completion/README.md, " +
            $"then set {_updateEnvironmentVariable}=true and rerun this test to regenerate the fixture.");
    }

    [Fact]
    public async Task ContractVectors_OnlyCommittedMutationsProduceReceipts()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        ApplicationSaveChangesPipeline pipeline = CreateAuditingPipeline();
        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // No mutation: saving an unchanged context completes no batch and produces no receipt.
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Null(pipeline.LastCompletedReceipt);

        // Committed mutation: the only persistence outcome NCAT emits.
        _ = context.ExternalLoginAccounts.Add(CreateAccount("committed-user"));
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        ApplicationMutationAuditReceipt receipt = Assert.IsType<ApplicationMutationAuditReceipt>(pipeline.LastCompletedReceipt);
        Assert.Equal(SupportedPersistenceOutcomes.Single(), receipt.PersistenceOutcome);

        // Failed or rolled-back mutation: the staged outbox handoff rolls back with the business transaction.
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, []);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync(
            (dbContext, cancellationToken) =>
            {
                _ = dbContext.ExternalLoginAccounts.Add(CreateAccount("rolled-back-user"));
                return Task.CompletedTask;
            },
            async (dbContext, stagedReceipt, cancellationToken) =>
            {
                _ = await outbox.StageAsync(dbContext, stagedReceipt, cancellationToken: cancellationToken);
                throw new InvalidOperationException("Synthetic failure after staging.");
            },
            cancellationToken: TestContext.Current.CancellationToken));

        context.ChangeTracker.Clear();
        Assert.Equal(0, await context.ApplicationAuditCompletionOutboxEntries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.ExternalLoginAccounts.CountAsync(TestContext.Current.CancellationToken));
    }

    private static string[] SupportedPersistenceOutcomes => ["Committed"];

    private static async Task<JsonObject> BuildContractDocumentAsync()
    {
        var builder = new CanonicalApplicationMutationManifestBuilder();
        var hasher = new Sha256ApplicationMutationManifestHasher();
        var verifier = new ApplicationMutationManifestVerifier(builder, hasher);

        var vectors = new JsonArray();
        ProducedVector? baseline = null;

        foreach (VectorDefinition definition in CreateVectorDefinitions())
        {
            ProducedVector produced = await ProduceVectorAsync(definition, builder, hasher, verifier);
            baseline ??= produced;
            vectors.Add(produced.ToJson());
        }

        return new JsonObject
        {
            ["contract"] = "ncat.audit-completion",
            ["contractVersion"] = _contractVersion,
            ["generation"] = new JsonObject
            {
                ["repository"] = "AsiBackbone/NetCoreApplicationTemplate",
                ["generator"] = "tests/ProjectTemplate.Web.Tests/AuditCompletionContractVectorTests.cs",
                ["regenerate"] = $"{_updateEnvironmentVariable}=true dotnet test --configuration Release",
                ["valuesAreSynthetic"] = true
            },
            ["schemaVersions"] = new JsonObject
            {
                ["completionMessage"] = ApplicationAuditCompletionOutboxEntry.CurrentSchemaVersion,
                ["mutationManifest"] = ApplicationMutationManifest.CurrentSchemaVersion,
                ["auditRecord"] = new AuditRecord().SchemaVersion
            },
            ["manifest"] = new JsonObject
            {
                ["algorithm"] = hasher.Algorithm,
                ["digestEncoding"] = "uppercase-hex",
                ["byteEncoding"] = "utf-8-without-bom"
            },
            ["idempotency"] = new JsonObject
            {
                ["keyPrefix"] = "ncat-audit-completion:",
                ["hashInput"] = "<destination>\\n<mutationBatchId>",
                ["hashAlgorithm"] = "SHA-256",
                ["digestEncoding"] = "uppercase-hex"
            },
            ["persistenceOutcomes"] = new JsonObject
            {
                ["supported"] = new JsonArray([.. SupportedPersistenceOutcomes.Select(outcome => (JsonNode?)outcome)]),
                ["withoutReceipt"] = new JsonArray(
                    OutcomeWithoutReceipt("no-mutation", "SaveChanges with no audited changes completes no mutation batch; no receipt or message is produced."),
                    OutcomeWithoutReceipt("failed", "A failed mutation or local completion throws before commit; the staged outbox entry is rolled back and no message is produced."),
                    OutcomeWithoutReceipt("rolled-back", "A rolled-back transaction discards the staged outbox entry; no message is produced."))
            },
            ["vectors"] = vectors,
            ["invalidMessages"] = await BuildInvalidMessagesAsync(baseline!, verifier)
        };
    }

    private static IEnumerable<VectorDefinition> CreateVectorDefinitions()
    {
        yield return new VectorDefinition(
            "committed-single-record-all-identifiers",
            "One created record with every optional correlation identifier supplied.",
            _defaultDestination,
            "synthetic-batch-0001",
            new AuditContextIds("operation-0001", "attempt-0001", "decision-0001", "correlation-0001", "0af7651916cd43dd8448eb211c80319c"),
            [
                new RecordDefinition(
                    "SyntheticOrder",
                    "Added",
                    /*lang=json,strict*/ """{"Id":"00000000-0000-0000-0000-000000000001"}""",
                    "",
                    /*lang=json,strict*/ """{"Status":"Open","Total":12.50}""",
                    "b7ad6b7169203331")
            ]);

        yield return new VectorDefinition(
            "committed-multi-record-canonical-ordering",
            "Unordered records, unordered properties, nested values, escaping, and empty payloads that exercise canonicalization.",
            _defaultDestination,
            "synthetic-batch-0002",
            new AuditContextIds("operation-0002", "attempt-0002", null, "correlation-0002", "4bf92f3577b34da6a3ce929d0e0e4736"),
            [
                new RecordDefinition(
                    "SyntheticOrder",
                    "Modified",
                    /*lang=json,strict*/ """{"Id":"00000000-0000-0000-0000-000000000002"}""",
                    /*lang=json,strict*/ """{"Total":1.50,"Status":"Open","Tags":["b","a"]}""",
                    /*lang=json,strict*/ """{"Tags":["b","a"],"Total":2.0,"Status":"Closed"}""",
                    "00f067aa0ba902b7"),
                new RecordDefinition(
                    "SyntheticCustomer",
                    "Deleted",
                    /*lang=json,strict*/ """{"Id":"00000000-0000-0000-0000-000000000003"}""",
                    /*lang=json,strict*/ """{"Name":"***","Note":"café <a+b> & 'quoted'","Profile":{"z":null,"a":true}}""",
                    "",
                    "00f067aa0ba902b8"),
                new RecordDefinition(
                    "SyntheticCustomer",
                    "Added",
                    /*lang=json,strict*/ """{"Id":"00000000-0000-0000-0000-000000000004"}""",
                    "   ",
                    /*lang=json,strict*/ """{"Name":"***","Profile":{"b":[1,2,{"y":1,"x":0}],"a":false}}""",
                    "00f067aa0ba902b9")
            ]);

        yield return new VectorDefinition(
            "committed-null-identifiers-custom-destination",
            "A system mutation without operation, attempt, decision, correlation, or trace identifiers delivered to a named destination.",
            _archiveDestination,
            "synthetic-batch-0003",
            new AuditContextIds(null, null, null, null, null),
            [
                new RecordDefinition(
                    "SyntheticSetting",
                    "Modified",
                    /*lang=json,strict*/ """{"Key":"synthetic.setting"}""",
                    /*lang=json,strict*/ """{"Value":"before"}""",
                    /*lang=json,strict*/ """{"Value":"after"}""",
                    null)
            ]);
    }

    private static async Task<ProducedVector> ProduceVectorAsync(
        VectorDefinition definition,
        CanonicalApplicationMutationManifestBuilder builder,
        Sha256ApplicationMutationManifestHasher hasher,
        ApplicationMutationManifestVerifier verifier)
    {
        List<AuditRecord> records = [.. definition.Records.Select(record => record.ToAuditRecord(definition))];
        ApplicationMutationManifest manifest = builder.Build(records);
        string digest = hasher.ComputeHash(manifest);

        var receipt = new ApplicationMutationAuditReceipt(
            manifest.MutationBatchId,
            manifest.AuditRecordCount,
            SupportedPersistenceOutcomes.Single(),
            _completedUtc,
            digest,
            hasher.Algorithm,
            manifest.SchemaVersion,
            definition.Ids.OperationExecutionId,
            definition.Ids.ExecutionAttemptId,
            definition.Ids.DecisionAuditRecordId,
            definition.Ids.CorrelationId,
            definition.Ids.TraceId);

        Assert.True(verifier.Verify(receipt, records));

        ApplicationAuditCompletionMessage message = await StageAndDispatchAsync(receipt, definition.Destination);
        return new ProducedVector(definition, records, manifest, digest, receipt, message);
    }

    private static async Task<ApplicationAuditCompletionMessage> StageAndDispatchAsync(
        ApplicationMutationAuditReceipt receipt,
        string destination)
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection, CreateNonAuditingPipeline());
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingPublisher(destination);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, [publisher]);
        _ = await outbox.StageAsync(context, receipt, destination, TestContext.Current.CancellationToken);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        ApplicationAuditCompletionDispatchSummary summary = await outbox.DispatchReadyAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, summary.DeliveredCount);
        return Assert.Single(publisher.Messages);
    }

    private static async Task<JsonArray> BuildInvalidMessagesAsync(
        ProducedVector baseline,
        ApplicationMutationManifestVerifier verifier)
    {
        ApplicationAuditCompletionMessage valid = baseline.Message;
        string otherDigest = new Sha256ApplicationMutationManifestHasher().ComputeHash(
            new ApplicationMutationManifest(ApplicationMutationManifest.CurrentSchemaVersion, valid.MutationBatchId, 1, "{}"));

        var invalid = new JsonArray
        {
            // Rejected by NCAT manifest verification against the vector's retained records.
            VerifierRejected(baseline, verifier, "manifest-digest-mismatch",
                "The digest does not match the canonical manifest of the retained batch.",
                valid with { MutationManifestHash = otherDigest }),
            VerifierRejected(baseline, verifier, "manifest-digest-not-hex",
                "The digest is not hexadecimal.",
                valid with { MutationManifestHash = "NOT-A-HEX-DIGEST" }),
            VerifierRejected(baseline, verifier, "manifest-algorithm-unsupported",
                "The manifest algorithm is not the algorithm that produced the digest.",
                valid with { MutationManifestAlgorithm = "SHA-1" }),
            VerifierRejected(baseline, verifier, "audit-record-count-mismatch",
                "The audit record count does not match the retained batch.",
                valid with { AuditRecordCount = valid.AuditRecordCount + 1 }),

            // Rejected by NCAT outbox staging.
            await StagingRejectedAsync(baseline, "mutation-batch-id-missing",
                "The mutation batch identifier is empty.",
                valid with { MutationBatchId = "" }),
            await StagingRejectedAsync(baseline, "destination-too-long",
                "The destination exceeds 128 characters.",
                valid with { Destination = new string('d', 129) }),

            // Never produced by NCAT for this contract version; adapters must reject them.
            Invalid("idempotency-key-mismatch",
                "The idempotency key is not derived from the destination and mutation batch identifier.",
                "adapter",
                AssertIdempotencyMismatch(valid, valid with { IdempotencyKey = "ncat-audit-completion:" + otherDigest })),
            Invalid("persistence-outcome-unsupported",
                "Only supported persistence outcomes are emitted; failed, rolled-back, and no-mutation saves produce no message.",
                "adapter",
                AssertNotProduced(valid with { PersistenceOutcome = "RolledBack" },
                    message => !SupportedPersistenceOutcomes.Contains(message.PersistenceOutcome, StringComparer.Ordinal))),
            Invalid("message-schema-version-unsupported",
                "The completion message schema version is not supported by this contract version.",
                "adapter",
                AssertNotProduced(valid with { SchemaVersion = "2.0" },
                    message => message.SchemaVersion != ApplicationAuditCompletionOutboxEntry.CurrentSchemaVersion)),
            Invalid("manifest-schema-version-unsupported",
                "The canonical manifest schema version is not supported by this contract version.",
                "adapter",
                AssertNotProduced(valid with { MutationManifestSchemaVersion = "2.0" },
                    message => message.MutationManifestSchemaVersion != ApplicationMutationManifest.CurrentSchemaVersion)),
            Invalid("optional-identifier-whitespace",
                "Optional identifiers are either null or non-whitespace; whitespace-only values are malformed.",
                "adapter",
                AssertNotProduced(valid with { CorrelationId = "   " },
                    message => message.CorrelationId is { Length: > 0 } correlation && string.IsNullOrWhiteSpace(correlation)))
        };

        return invalid;
    }

    private static JsonObject VerifierRejected(
        ProducedVector baseline,
        ApplicationMutationManifestVerifier verifier,
        string name,
        string rule,
        ApplicationAuditCompletionMessage message)
    {
        ApplicationMutationAuditReceipt receipt = new(
            message.MutationBatchId,
            message.AuditRecordCount,
            message.PersistenceOutcome,
            new DateTimeOffset(DateTime.SpecifyKind(message.ReceiptCompletedUtc, DateTimeKind.Utc)),
            message.MutationManifestHash,
            message.MutationManifestAlgorithm,
            message.MutationManifestSchemaVersion,
            message.OperationExecutionId,
            message.ExecutionAttemptId,
            message.DecisionAuditRecordId,
            message.CorrelationId,
            message.TraceId);

        Assert.False(verifier.Verify(receipt, baseline.Records));
        return Invalid(name, rule, "ncat-manifest-verifier", message);
    }

    private static async Task<JsonObject> StagingRejectedAsync(
        ProducedVector baseline,
        string name,
        string rule,
        ApplicationAuditCompletionMessage message)
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection, CreateNonAuditingPipeline());
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, []);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await outbox.StageAsync(
            context,
            baseline.Receipt with { MutationBatchId = message.MutationBatchId },
            message.Destination,
            TestContext.Current.CancellationToken));

        return Invalid(name, rule, "ncat-outbox-staging", message);
    }

    private static ApplicationAuditCompletionMessage AssertIdempotencyMismatch(
        ApplicationAuditCompletionMessage valid,
        ApplicationAuditCompletionMessage invalid)
    {
        Assert.NotEqual(valid.IdempotencyKey, invalid.IdempotencyKey);
        return invalid;
    }

    private static ApplicationAuditCompletionMessage AssertNotProduced(
        ApplicationAuditCompletionMessage message,
        Func<ApplicationAuditCompletionMessage, bool> violatesContract)
    {
        Assert.True(violatesContract(message));
        return message;
    }

    private static JsonObject Invalid(string name, string rule, string enforcedBy, ApplicationAuditCompletionMessage message)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["rule"] = rule,
            ["enforcedBy"] = enforcedBy,
            ["message"] = JsonSerializer.SerializeToNode(message, _contractJsonOptions)
        };
    }

    private static JsonObject OutcomeWithoutReceipt(string scenario, string behavior)
    {
        return new JsonObject
        {
            ["scenario"] = scenario,
            ["behavior"] = behavior
        };
    }

    private static string GetFixturePath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.EnumerateFiles(directory.FullName, "*.slnx").Any())
            {
                return Path.Combine(
                    directory.FullName,
                    "contracts",
                    "audit-completion",
                    "v" + _contractVersion.Split('.')[0],
                    "audit-completion-vectors.json");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate solution root from '{AppContext.BaseDirectory}'.");
    }

    private static ApplicationSaveChangesPipeline CreateAuditingPipeline()
    {
        return new ApplicationSaveChangesPipeline(
            new TestCurrentActorAccessor(),
            Microsoft.Extensions.Options.Options.Create(new DataAccessOptions
            {
                Auditing = new DataAuditingOptions
                {
                    Enabled = true,
                    StorageMode = AuditStorageModes.Local
                }
            }));
    }

    private static ApplicationSaveChangesPipeline CreateNonAuditingPipeline()
    {
        return new ApplicationSaveChangesPipeline(
            new TestCurrentActorAccessor(),
            Microsoft.Extensions.Options.Options.Create(new DataAccessOptions
            {
                Auditing = new DataAuditingOptions { Enabled = false }
            }));
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection, ApplicationSaveChangesPipeline pipeline)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        return new ApplicationDbContext(
            options,
            NullLogger<ApplicationDbContext>.Instance,
            new ApplicationSaveChangesInterceptor(pipeline));
    }

    private static ApplicationAuditCompletionOutbox CreateOutbox(
        ApplicationDbContext context,
        IEnumerable<IApplicationAuditCompletionPublisher> publishers)
    {
        return new ApplicationAuditCompletionOutbox(
            context,
            Microsoft.Extensions.Options.Options.Create(new ApplicationAuditCompletionOutboxOptions
            {
                DefaultDestination = _defaultDestination
            }),
            publishers,
            new FixedTimeProvider(_stagedUtc));
    }

    private static ExternalLoginAccount CreateAccount(string providerUserId)
    {
        return new ExternalLoginAccount
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "synthetic",
            ProviderUserId = providerUserId,
            DisplayName = "Synthetic Contract User",
            Email = $"{providerUserId}@example.com",
            CreatedOnUtc = _completedUtc.UtcDateTime
        };
    }

    private sealed record AuditContextIds(
        string? OperationExecutionId,
        string? ExecutionAttemptId,
        string? DecisionAuditRecordId,
        string? CorrelationId,
        string? TraceId);

    private sealed record RecordDefinition(
        string Entity,
        string State,
        string KeyValues,
        string OriginalValues,
        string CurrentValues,
        string? SpanId)
    {
        public AuditRecord ToAuditRecord(VectorDefinition vector)
        {
            return new AuditRecord
            {
                MutationBatchId = vector.MutationBatchId,
                Entity = Entity,
                State = State,
                KeyValues = KeyValues,
                OriginalValues = OriginalValues,
                CurrentValues = CurrentValues,
                OperationExecutionId = vector.Ids.OperationExecutionId,
                ExecutionAttemptId = vector.Ids.ExecutionAttemptId,
                DecisionAuditRecordId = vector.Ids.DecisionAuditRecordId,
                CorrelationId = vector.Ids.CorrelationId,
                TraceId = vector.Ids.TraceId,
                SpanId = SpanId
            };
        }
    }

    private sealed record VectorDefinition(
        string Name,
        string Description,
        string Destination,
        string MutationBatchId,
        AuditContextIds Ids,
        IReadOnlyList<RecordDefinition> Records);

    private sealed record ProducedVector(
        VectorDefinition Definition,
        IReadOnlyCollection<AuditRecord> Records,
        ApplicationMutationManifest Manifest,
        string Digest,
        ApplicationMutationAuditReceipt Receipt,
        ApplicationAuditCompletionMessage Message)
    {
        public JsonObject ToJson()
        {
            return new JsonObject
            {
                ["name"] = Definition.Name,
                ["description"] = Definition.Description,
                ["auditRecords"] = new JsonArray([.. Records.Select(record => (JsonNode?)new JsonObject
                {
                    ["schemaVersion"] = record.SchemaVersion,
                    ["mutationBatchId"] = record.MutationBatchId,
                    ["entity"] = record.Entity,
                    ["state"] = record.State,
                    ["keyValues"] = record.KeyValues,
                    ["originalValues"] = record.OriginalValues,
                    ["currentValues"] = record.CurrentValues,
                    ["operationExecutionId"] = record.OperationExecutionId,
                    ["executionAttemptId"] = record.ExecutionAttemptId,
                    ["decisionAuditRecordId"] = record.DecisionAuditRecordId,
                    ["correlationId"] = record.CorrelationId,
                    ["traceId"] = record.TraceId,
                    ["spanId"] = record.SpanId
                })]),
                ["canonicalManifest"] = new JsonObject
                {
                    ["schemaVersion"] = Manifest.SchemaVersion,
                    ["json"] = Manifest.CanonicalJson,
                    ["utf8ByteLength"] = Encoding.UTF8.GetByteCount(Manifest.CanonicalJson),
                    ["expectedDigest"] = Digest
                },
                ["receipt"] = JsonSerializer.SerializeToNode(Receipt, _contractJsonOptions),
                ["message"] = JsonSerializer.SerializeToNode(Message, _contractJsonOptions)
            };
        }
    }

    private sealed class RecordingPublisher(string destination) : IApplicationAuditCompletionPublisher
    {
        public string Destination { get; } = destination;

        public List<ApplicationAuditCompletionMessage> Messages { get; } = [];

        public ValueTask<ApplicationAuditCompletionPublishResult> PublishAsync(
            ApplicationAuditCompletionMessage message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add(message);
            return ValueTask.FromResult(ApplicationAuditCompletionPublishResult.Success());
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "Audit Completion Contract Vector Test";
    }
}
