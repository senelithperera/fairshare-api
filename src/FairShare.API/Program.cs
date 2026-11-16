using System.Net.Sockets;
using System.Threading.RateLimiting;
using FairShare.API.Middleware;
using FairShare.Application.Extensions;
using FairShare.Infrastructure.Extensions;
using FairShare.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─── Hosting ────────────────────────────────────────────────────────────────
// Hosting platforms (Render, Cloud Run, …) say which port to listen on via PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://+:{port}");

// ─── Serilog ────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
      .Enrich.FromLogContext()
      .WriteTo.Console();

    // Log files only locally: hosted containers have no persistent disk, and the platform
    // collects console output instead.
    if (ctx.HostingEnvironment.IsDevelopment())
    {
        lc.WriteTo.File(
            path: "logs/fairshare-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14);
    }
});

// ─── Services ───────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FairShare API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description  = "Enter your JWT token."
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var allowedOrigins = builder.Configuration["AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// Behind a hosting proxy (Render, …) the real client IP and scheme arrive in X-Forwarded-*
// headers. The proxy's address isn't known in advance, so trust exactly one hop: the
// platform's proxy appends the real client IP as the last entry.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 1;
});

// Per-IP rate limits, so a public demo link can't be used to flood the database.
// Writes (create/update/delete) are limited more tightly than reads.
var writesPerMinute = builder.Configuration.GetValue("RateLimit:WritesPerMinute", 30);
var readsPerMinute  = builder.Configuration.GetValue("RateLimit:ReadsPerMinute", 120);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, ct) => new ValueTask(
        context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                type   = "https://tools.ietf.org/html/rfc6585#section-4",
                title  = "Too many requests. Please wait a minute and try again.",
                status = StatusCodes.Status429TooManyRequests,
            },
            options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json",
            cancellationToken: ct));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
    {
        if (http.Request.Path.StartsWithSegments("/health"))
            return RateLimitPartition.GetNoLimiter("health");

        var method  = http.Request.Method;
        var isWrite = !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));
        var ip      = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            $"{ip}:{(isWrite ? "write" : "read")}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = isWrite ? writesPerMinute : readsPerMinute,
                Window      = TimeSpan.FromMinutes(1),
                QueueLimit  = 0,
            });
    });
});

// ─── App ────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c => c.RouteTemplate = "openapi/{documentName}.json");
    app.MapScalarApiReference(options =>
    {
        options.Title          = "FairShare API";
        options.Theme          = ScalarTheme.Purple;
        options.OpenApiRoutePattern = "/openapi/{documentName}.json";
    });
}
else if (allowedOrigins.Length == 0)
{
    app.Logger.LogWarning(
        "AllowedOrigins is not set: browsers on other origins (the web app) can't call this API. " +
        "Set the AllowedOrigins environment variable to the web app's URL.");
}

// No UseHttpsRedirection: the hosting platform terminates TLS and redirects HTTP to HTTPS at
// its edge; inside the container the app only sees plain HTTP (and would just log warnings).
app.UseCors();         // before the rate limiter, so 429 responses still carry CORS headers
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Liveness check for the hosting platform. Deliberately doesn't touch the database, so
// health pings don't keep a serverless database awake.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

await MigrateWithRetryAsync(app);

app.Run();

// Applies pending migrations, retrying while the database is unreachable — a serverless
// database (e.g. Neon) can take a moment to wake, and a crash here would fail the deploy.
static async Task MigrateWithRetryAsync(WebApplication app)
{
    const int maxAttempts = 6;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();
            await db.Database.MigrateAsync();
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts && IsConnectionFailure(ex))
        {
            var delay = TimeSpan.FromSeconds(Math.Min(2 * attempt, 10));
            app.Logger.LogWarning(
                "Database not reachable yet (attempt {Attempt}/{MaxAttempts}): {Message}. Retrying in {Delay}s.",
                attempt, maxAttempts, ex.Message, delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
}

static bool IsConnectionFailure(Exception ex)
{
    for (var e = ex; e is not null; e = e.InnerException)
    {
        if (e is NpgsqlException or SocketException or TimeoutException)
            return true;
    }
    return false;
}
