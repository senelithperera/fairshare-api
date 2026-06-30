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
        // EF Core — PostgreSQL
        services.AddDbContext<FairShareDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(FairShareDbContext).Assembly.FullName)));

        // Repositories
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IGroupMemberRepository, GroupMemberRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Application services
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

        // JWT Bearer authentication
        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

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
