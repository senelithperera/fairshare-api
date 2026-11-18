using FairShare.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace FairShare.Tests.Api;

/// <summary>Browser security headers added to every API response.</summary>
public class SecurityHeadersMiddlewareTests
{
    private static async Task<IHeaderDictionary> RunAsync(string path, RequestDelegate? next = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var middleware = new SecurityHeadersMiddleware(next ?? (_ => Task.CompletedTask));
        await middleware.InvokeAsync(context);
        return context.Response.Headers;
    }

    [Theory]
    [InlineData("/api/groups/7f1c2b9e-0d3a-4c55-9a51-2f4f1e7f0b11")]
    [InlineData("/health")]
    public async Task EveryResponse_GetsNoSniffNoFrameAndNoReferrer(string path)
    {
        var headers = await RunAsync(path);

        headers.XContentTypeOptions.ToString().Should().Be("nosniff");
        headers.XFrameOptions.ToString().Should().Be("DENY");
        headers["Referrer-Policy"].ToString().Should().Be("no-referrer");
        headers.ContentSecurityPolicy.ToString().Should().Be(SecurityHeadersMiddleware.ApiContentSecurityPolicy);
    }

    [Fact]
    public async Task ApiResponses_AreNeverCached()
    {
        var headers = await RunAsync("/api/groups/7f1c2b9e-0d3a-4c55-9a51-2f4f1e7f0b11/summary");

        headers.CacheControl.ToString().Should().Be("no-store");
    }

    [Fact]
    public async Task HealthCheck_IsNotMarkedNoStore()
    {
        var headers = await RunAsync("/health");

        headers.CacheControl.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.json")]
    public async Task DevApiReferencePages_SkipTheContentSecurityPolicy(string path)
    {
        var headers = await RunAsync(path);

        headers.ContentSecurityPolicy.ToString().Should().BeEmpty();
        headers.XContentTypeOptions.ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task HeadersAreSet_BeforeTheRestOfThePipelineRuns()
    {
        // So responses written further down (a 500 from ExceptionMiddleware, a 429 from the
        // rate limiter) already carry them.
        string? seenByNext = null;
        await RunAsync("/api/groups", ctx =>
        {
            seenByNext = ctx.Response.Headers.XContentTypeOptions.ToString();
            return Task.CompletedTask;
        });

        seenByNext.Should().Be("nosniff");
    }
}
