using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace FairShare.API.Extensions;

/// <summary>
/// Per-IP rate limits for a public API used by a circle of about a dozen friends.
/// </summary>
/// <remarks>
/// Friends travelling together are often all on one Wi-Fi network or phone hotspot, so they
/// share one IP address. Every limit here is sized for the whole group's busiest moment on one
/// IP (everyone adding the day's expenses at once), with room to spare, while still capping what
/// a script hammering the API can do:
/// <list type="bullet">
/// <item>Per minute: reads and writes, to absorb bursts.</item>
/// <item>Per day: writes only. This is the guard on database growth; 30 writes a minute would
/// otherwise still allow 40,000+ a day.</item>
/// <item>Creating groups: per hour, on top of the above. Friends make a group per trip.</item>
/// </list>
/// All values are configurable under <c>RateLimit</c>; see README, Security.
/// </remarks>
public static class RateLimitingExtensions
{
    /// <summary>Name of the extra limit on creating groups (applied on the endpoint).</summary>
    public const string GroupCreatePolicy = "group-create";

    public static IServiceCollection AddFairShareRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        var readsPerMinute  = config.GetValue("RateLimit:ReadsPerMinute", 300);
        var writesPerMinute = config.GetValue("RateLimit:WritesPerMinute", 60);
        var writesPerDay    = config.GetValue("RateLimit:WritesPerDay", 1000);
        var groupsPerHour   = config.GetValue("RateLimit:GroupsPerHour", 10);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, ct) =>
            {
                TimeSpan? retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : null;
                if (retryAfter is { } r)
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(r.TotalSeconds)).ToString();

                return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        type   = "https://tools.ietf.org/html/rfc6585#section-4",
                        // Shown to people as-is by the web app.
                        title  = RejectionMessage(retryAfter),
                        status = StatusCodes.Status429TooManyRequests,
                    },
                    options: (System.Text.Json.JsonSerializerOptions?)null,
                    contentType: "application/problem+json",
                    cancellationToken: ct));
            };

            var perMinute = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                if (http.Request.Path.StartsWithSegments("/health"))
                    return RateLimitPartition.GetNoLimiter("health");

                var write = IsWrite(http);
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"{ClientIp(http)}:{(write ? "write" : "read")}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = write ? writesPerMinute : readsPerMinute,
                        Window      = TimeSpan.FromMinutes(1),
                        QueueLimit  = 0,
                    });
            });

            var writesDaily = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                !IsWrite(http)
                    ? RateLimitPartition.GetNoLimiter("reads")
                    : RateLimitPartition.GetFixedWindowLimiter(
                        $"{ClientIp(http)}:day",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = writesPerDay,
                            Window      = TimeSpan.FromDays(1),
                            QueueLimit  = 0,
                        }));

            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(perMinute, writesDaily);

            options.AddPolicy(GroupCreatePolicy, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientIp(http),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = groupsPerHour,
                        Window      = TimeSpan.FromHours(1),
                        QueueLimit  = 0,
                    }));
        });

        return services;
    }

    /// <summary>What a person sees when they hit a limit: how long to wait, in plain words.</summary>
    public static string RejectionMessage(TimeSpan? retryAfter)
    {
        var wait = retryAfter switch
        {
            null                                   => "a little while",
            { TotalSeconds: <= 60 }                => "a minute",
            { TotalMinutes: < 60 } r               => $"{Math.Ceiling(r.TotalMinutes)} minutes",
            { TotalHours: < 1.5 }                  => "an hour",
            { } r                                  => $"{Math.Ceiling(r.TotalHours)} hours",
        };
        return $"Too many requests from this network. Try again in {wait}.";
    }

    private static bool IsWrite(HttpContext http)
    {
        var method = http.Request.Method;
        return !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));
    }

    // The real client IP: UseForwardedHeaders has already applied the hosting proxy's X-Forwarded-For.
    private static string ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
