using FairShare.API.Extensions;
using FairShare.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairShare.Tests.Api;

/// <summary>What people are told when a request is refused for being too big or too frequent.</summary>
public class RequestLimitsTests
{
    [Theory]
    [InlineData(20, "Try again in a minute.")]
    [InlineData(60, "Try again in a minute.")]
    [InlineData(25 * 60, "Try again in 25 minutes.")]
    [InlineData(60 * 60, "Try again in an hour.")]
    [InlineData(24 * 60 * 60, "Try again in 24 hours.")]
    public void RateLimitMessage_SaysHowLongToWait(int seconds, string ending)
    {
        RateLimitingExtensions.RejectionMessage(TimeSpan.FromSeconds(seconds))
            .Should().Be($"Too many requests from this network. {ending}");
    }

    [Fact]
    public void RateLimitMessage_WithoutAWaitTime_StillReadsNaturally()
    {
        RateLimitingExtensions.RejectionMessage(null)
            .Should().Be("Too many requests from this network. Try again in a little while.");
    }

    private static async Task<HttpContext> RunAsync(Exception thrown)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(_ => throw thrown, NullLogger<ExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        return context;
    }

    [Fact]
    public async Task OversizedBody_Is413_NotAServerError()
    {
        var context = await RunAsync(new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

        context.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        context.Response.Body.Position = 0;
        new StreamReader(context.Response.Body).ReadToEnd().Should().Contain("That request is too large.");
    }

    [Fact]
    public async Task OtherFailures_AreStill500()
    {
        var context = await RunAsync(new InvalidOperationException("boom"));

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
