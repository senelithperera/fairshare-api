using FairShare.Application.UseCases.Auth;
using FairShare.Application.UseCases.Expenses;
using FairShare.Application.UseCases.Groups;
using FairShare.Application.UseCases.Users;
using FairShare.Application.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FairShare.Application.Extensions;

/// <summary>Registers all Application-layer services with the DI container.</summary>
public static class ApplicationServiceExtensions
{
    /// <summary>Adds use cases and validators from <c>FairShare.Application</c>.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Validators — scan current assembly
        services.AddValidatorsFromAssemblyContaining<CreateGroupValidator>();

        // Group use cases
        services.AddScoped<CreateGroupUseCase>();
        services.AddScoped<GetGroupUseCase>();
        services.AddScoped<AddMemberUseCase>();
        services.AddScoped<UpdateMemberUseCase>();
        services.AddScoped<DeleteMemberUseCase>();

        // Expense use cases
        services.AddScoped<CreateExpenseUseCase>();
        services.AddScoped<UpdateExpenseUseCase>();
        services.AddScoped<DeleteExpenseUseCase>();
        services.AddScoped<GetSummaryUseCase>();

        // Auth use cases
        services.AddScoped<RegisterUseCase>();
        services.AddScoped<LoginUseCase>();
        services.AddScoped<RefreshTokenUseCase>();
        services.AddScoped<LogoutUseCase>();

        // User use cases
        services.AddScoped<GetCurrentUserUseCase>();
        services.AddScoped<UpdateCurrentUserUseCase>();
        services.AddScoped<GetUserGroupsUseCase>();

        return services;
    }
}
