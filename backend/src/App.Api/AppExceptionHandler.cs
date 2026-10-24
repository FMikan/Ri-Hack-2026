using App.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace App.Api;

// Game-rule and auth violations become Problem Details with type "urn:app:<code>"; the frontend branches on that type.
public class AppExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppException app) return false;

        httpContext.Response.StatusCode = app.Status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Type = app.Code, Title = app.Message, Detail = app.Message, Status = app.Status },
        });
    }
}
