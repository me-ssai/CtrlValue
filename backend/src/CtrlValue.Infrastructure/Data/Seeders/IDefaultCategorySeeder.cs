namespace CtrlValue.Infrastructure.Data.Seeders;

/// <summary>
/// Seeds the standard set of categories and keyword rules for a newly created entity.
/// Replaces the Supabase trigger create_default_categories_for_entity_when_created.
/// </summary>
public interface IDefaultCategorySeeder
{
    Task SeedAsync(Guid entityId, string tenantId, CancellationToken ct = default);
}
