using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CtrlValue.Api.Infrastructure;

/// <summary>
/// Global action filter that rejects all mutating requests (POST, PUT, PATCH, DELETE)
/// when the request is identified as a demo session by <see cref="DemoRequestContext"/>.
///
/// This is the server-side defence-in-depth layer. The frontend also intercepts writes
/// client-side (via DemoHttpInterceptor) so visitors never see an error — but even if
/// someone bypasses the frontend and calls the API directly, this filter ensures no
/// data is ever persisted for the demo tenant.
/// </summary>
public class BlockDemoWritesFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> WriteMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var demoCtx = context.HttpContext.RequestServices.GetRequiredService<DemoRequestContext>();

        if (demoCtx.IsDemo && WriteMethods.Contains(context.HttpContext.Request.Method))
        {
            context.Result = new ObjectResult(new
            {
                demoMode = true,
                message  = "Changes are not persisted in demo mode. Create a free account to save your data."
            })
            { StatusCode = StatusCodes.Status403Forbidden };

            return;
        }

        await next();
    }
}
