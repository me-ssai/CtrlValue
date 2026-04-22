namespace CtrlValue.Domain.Entities;

/// <summary>
/// Join table that tracks which default instruments a given entity has been subscribed to.
/// </summary>
public class EntityDefaultTicker : BaseEntity
{
    public Guid EntityId { get; set; }
    public Guid InstrumentId { get; set; }

    // Navigation
    public Entity Entity { get; set; } = null!;
    public Instrument Instrument { get; set; } = null!;
}
