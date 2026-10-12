using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Entities;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the input guards and null-field canonicalization of <c>CanonicalApplicationMutationManifestBuilder</c>.
/// </summary>
/// <remarks>
/// The <c>Undefined</c> and unknown JSON value-kind branches are not reachable, because a parsed
/// <c>JsonDocument</c> never yields those kinds.
/// </remarks>
public sealed class CanonicalApplicationMutationManifestBuilderBranchCoverageTests
{
    private readonly CanonicalApplicationMutationManifestBuilder _builder = new();

    [Fact]
    public void Build_NoRecords_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => _builder.Build([]));

        Assert.Equal("auditRecords", exception.ParamName);
    }

    [Fact]
    public void Build_BlankMutationBatchId_ThrowsInvalidOperationException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            _builder.Build([CreateRecord(" ")]));

        Assert.Contains("mutation batch identifier", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RecordsFromDifferentBatches_ThrowsInvalidOperationException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            _builder.Build([CreateRecord("manifest-batch-a"), CreateRecord("manifest-batch-b")]));

        Assert.Contains("only one mutation batch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NonObjectValuePayload_ThrowsInvalidOperationException()
    {
        AuditRecord record = CreateRecord("manifest-array-payload");
        record.KeyValues = /*lang=json,strict*/ "[1,2]";

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            _builder.Build([record]));

        Assert.Contains("must be JSON objects", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NullTextFields_CanonicalizesThemAsEmptyValues()
    {
        AuditRecord record = CreateRecord("manifest-null-fields");
        record.SchemaVersion = null!;
        record.Entity = null!;
        record.State = null!;
        record.KeyValues = null!;

        ApplicationMutationManifest manifest = _builder.Build([record]);

        Assert.Equal(1, manifest.AuditRecordCount);
        Assert.Contains("\"entity\":\"\"", manifest.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"state\":\"\"", manifest.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"keyValues\":{}", manifest.CanonicalJson, StringComparison.Ordinal);
    }

    private static AuditRecord CreateRecord(string mutationBatchId)
    {
        return new AuditRecord
        {
            SchemaVersion = "1.0",
            Entity = "Example",
            State = "Modified",
            MutationBatchId = mutationBatchId,
            KeyValues = /*lang=json,strict*/ "{\"Id\":\"1\"}",
            OriginalValues = /*lang=json,strict*/ "{\"Value\":\"before\"}",
            CurrentValues = /*lang=json,strict*/ "{\"Value\":\"after\"}"
        };
    }
}
