using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GroceryStore.Api.Common.Security;

/// <summary>
/// CSRF defence for endpoints that accept non-JSON bodies (file uploads). An HTML form on another site
/// can submit multipart/form-data, but it cannot add custom headers: a cross-site fetch with one triggers
/// a CORS preflight, which this API never approves. The web app's apiClient always sends this header.
/// Complements the SameSite=Strict cookie (ADR 0002).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireXhrHeaderAttribute : Attribute, IResourceFilter
{
    public const string HeaderName = "X-Requested-With";
    public const string HeaderValue = "XMLHttpRequest";

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (context.HttpContext.Request.Headers[HeaderName] != HeaderValue)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad request",
                Detail = $"Missing {HeaderName} header.",
            })
            { StatusCode = StatusCodes.Status400BadRequest };
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
