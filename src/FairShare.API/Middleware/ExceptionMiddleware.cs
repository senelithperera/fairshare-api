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
        catch (BadHttpRequestException ex) when (!context.Response.HasStarted)
        {
            // A malformed or oversized request (e.g. a body over the 64 KB limit): the client's
            // fault, not ours, so answer with its own status code (413, 400, …), not a 500.
            _logger.LogWarning("Rejected {Method} {Path}: {Message}",
                context.Request.Method, context.Request.Path, ex.Message);

            await WriteProblemAsync(context, new ProblemDetails
            {
                Type   = "https://tools.ietf.org/html/rfc9110#section-15.5",
                Title  = ex.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "That request is too large."
                    : "That request couldn't be read.",
                Status = ex.StatusCode,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteProblemDetailsAsync(context, ex);
        }
    }

    private static Task WriteProblemDetailsAsync(HttpContext context, Exception ex)
        => WriteProblemAsync(context, new ProblemDetails
        {
            Type   = "https://tools.ietf.org/html/rfc7807",
            Title  = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "An internal error occurred. Please try again later.",
        });

    private static async Task WriteProblemAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode  = problem.Status ?? StatusCodes.Status500InternalServerError;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, options));
    }
}
