using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ProjectTemplate.Infrastructure.Data;

/// <summary>
/// Invokes the application save pipeline from the EF Core SaveChanges interception lifecycle.
/// </summary>
public sealed class ApplicationSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IApplicationSaveChangesPipeline _saveChangesPipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationSaveChangesInterceptor" /> class.
    /// </summary>
    /// <param name="saveChangesPipeline">The application-owned save pipeline.</param>
    public ApplicationSaveChangesInterceptor(IApplicationSaveChangesPipeline saveChangesPipeline)
    {
        ArgumentNullException.ThrowIfNull(saveChangesPipeline);

        _saveChangesPipeline = saveChangesPipeline;
    }

    /// <summary>
    /// Runs <see cref="IApplicationSaveChangesPipeline.ApplyBeforeSaveChanges" /> before EF Core persists the
    /// tracked changes of an <see cref="ApplicationDbContext" />. Other contexts pass through unchanged.
    /// </summary>
    /// <param name="eventData">Contextual information about the save operation.</param>
    /// <param name="result">The current interception result, returned unchanged.</param>
    /// <returns>The <paramref name="result" /> supplied by EF Core.</returns>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is ApplicationDbContext dbContext)
        {
            _ = _saveChangesPipeline.ApplyBeforeSaveChanges(dbContext);
        }

        return result;
    }

    /// <summary>
    /// Runs <see cref="IApplicationSaveChangesPipeline.ApplyBeforeSaveChangesAsync" /> before EF Core
    /// asynchronously persists the tracked changes of an <see cref="ApplicationDbContext" />. Other contexts pass
    /// through unchanged.
    /// </summary>
    /// <param name="eventData">Contextual information about the save operation.</param>
    /// <param name="result">The current interception result, returned unchanged.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The <paramref name="result" /> supplied by EF Core.</returns>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is ApplicationDbContext dbContext)
        {
            _ = await _saveChangesPipeline
                .ApplyBeforeSaveChangesAsync(dbContext, cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Runs <see cref="IApplicationSaveChangesPipeline.ApplyAfterSaveChanges" /> after EF Core persists the tracked
    /// changes of an <see cref="ApplicationDbContext" />, and saves again when the pipeline appended audit records
    /// that depend on database-generated values.
    /// </summary>
    /// <param name="eventData">Contextual information about the completed save operation.</param>
    /// <param name="result">The number of state entries written by the original save.</param>
    /// <returns>The <paramref name="result" /> supplied by EF Core.</returns>
    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        if (eventData.Context is ApplicationDbContext dbContext &&
            _saveChangesPipeline.ApplyAfterSaveChanges(dbContext))
        {
            _ = dbContext.SaveChanges();
        }

        return result;
    }

    /// <summary>
    /// Runs <see cref="IApplicationSaveChangesPipeline.ApplyAfterSaveChangesAsync" /> after EF Core asynchronously
    /// persists the tracked changes of an <see cref="ApplicationDbContext" />, and saves again when the pipeline
    /// appended audit records that depend on database-generated values.
    /// </summary>
    /// <param name="eventData">Contextual information about the completed save operation.</param>
    /// <param name="result">The number of state entries written by the original save.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The <paramref name="result" /> supplied by EF Core.</returns>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is ApplicationDbContext dbContext &&
            await _saveChangesPipeline
                .ApplyAfterSaveChangesAsync(dbContext, cancellationToken)
                .ConfigureAwait(false))
        {
            _ = await dbContext
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }
}
