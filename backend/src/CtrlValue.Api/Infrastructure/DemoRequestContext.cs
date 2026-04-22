namespace CtrlValue.Api.Infrastructure;

/// <summary>
/// Scoped service populated by <see cref="DemoRequestMiddleware"/> before any controller
/// runs. Controllers and services read IsDemo from here — never from request headers directly.
/// </summary>
public class DemoRequestContext
{
    public bool IsDemo { get; set; } = false;
}
