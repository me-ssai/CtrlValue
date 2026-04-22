using CtrlValue.Domain.Entities;

namespace CtrlValue.Application.Interfaces;

public interface IAiCategorizationService
{
    /// <summary>
    /// Categorises the supplied staging rows using the ProjectZAI service.
    /// Populates <see cref="ImportedTransactionsFileStaging.CategoryId"/> in-place
    /// for each row where a confident match is found.
    /// <para>
    /// This method is <strong>best-effort</strong>: if the AI service is unavailable,
    /// times out, or returns unusable data, it logs a warning and returns without
    /// throwing, leaving affected rows uncategorised.
    /// </para>
    /// </summary>
    /// <param name="rows">Valid staging rows to categorise (must already be persisted).</param>
    /// <param name="availableCategories">Full category list for the tenant.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task CategorizeAsync(
        IReadOnlyList<ImportedTransactionsFileStaging> rows,
        IReadOnlyList<Category> availableCategories,
        CancellationToken cancellationToken = default);
}
