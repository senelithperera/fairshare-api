using FairShare.Application.Interfaces;
using FairShare.Infrastructure.Persistence;
using FairShare.Infrastructure.Repositories;
using FairShare.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace FairShare.Infrastructure.Extensions;

/// <summary>Registers all Infrastructure-layer services with the DI container.</summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>Adds EF Core, repositories, auth services, and JWT bearer middleware.</summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core — PostgreSQL. Accepts a postgres:// URL too (what Neon/Render hand out).
        // Retries transient failures, e.g. while a serverless database wakes from sleep.
        services.AddDbContext<FairShareDbContext>(options =>
            options.UseNpgsql(
                PostgresConnectionString.Normalize(configuration.GetConnectionString("DefaultConnection")),
                npgsql => npgsql
                    .MigrationsAssembly(typeof(FairShareDbContext).Assembly.FullName)
                    .EnableRetryOnFailure(maxRetryCount: 5)));

        // Repositories
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IGroupMemberRepository, GroupMemberRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Application services
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

        // JWT Bearer authentication
        var jwtKey = JwtSigningKey.Require(configuration["Jwt:Key"]);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer           = true,
                    ValidIssuer              = configuration["Jwt:Issuer"],
                    ValidateAudience         = true,
                    ValidAudience            = configuration["Jwt:Audience"],
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.Zero
                };
            });

        services.AddAuthorization();

        return services;
    }
}
