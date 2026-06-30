using FairShare.Application.DTOs.Auth;
using FluentValidation;

namespace FairShare.Application.Validators;

/// <summary>Validates <see cref="LoginRequest"/>.</summary>
public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
