using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.API.Middleware;

/// <summary>
/// Global exception handler middleware that converts unhandled exceptions to
/// RFC 7807 <see cref="ProblemDetails"/> responses.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteProblemDetailsAsync(context, ex);
        }
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode  = StatusCodes.Status500InternalServerError;

        var problem = new ProblemDetails
        {
            Type   = "https://tools.ietf.org/html/rfc7807",
            Title  = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "An internal error occurred. Please try again later.",
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, options));
    }
}
