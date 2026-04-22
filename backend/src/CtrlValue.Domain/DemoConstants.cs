namespace CtrlValue.Domain;

public static class DemoConstants
{
    /// <summary>
    /// Well-known, fixed Guid for the demo Entity row.
    /// Hard-coded so it can be referenced at compile time across all layers
    /// without a database lookup. Must match the value in appsettings Demo:EntityId.
    /// </summary>
    public static readonly Guid DemoEntityId =
        new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public const string DemoRole = "demo";
    public const string DemoTenantId = "demo";
    public const string DemoEntityName = "Demo Workspace";
}
