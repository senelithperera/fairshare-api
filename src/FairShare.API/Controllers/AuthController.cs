using FairShare.Application.DTOs.Auth;
using FairShare.Application.UseCases.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.API.Controllers;

/// <summary>Handles user registration, login, token refresh, and logout.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly RegisterUseCase _register;
    private readonly LoginUseCase _login;
    private readonly RefreshTokenUseCase _refresh;
    private readonly LogoutUseCase _logout;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthController(
        RegisterUseCase register,
        LoginUseCase login,
        RefreshTokenUseCase refresh,
        LogoutUseCase logout,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _register          = register;
        _login             = login;
        _refresh           = refresh;
        _logout            = logout;
        _registerValidator = registerValidator;
        _loginValidator    = loginValidator;
    }

    /// <summary>Registers a new user account.</summary>
    /// <response code="201">Registration successful. Returns access and refresh tokens.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="409">Email is already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var (result, response) = await _register.ExecuteAsync(request, ct);

        return result switch
        {
            RegisterResult.EmailAlreadyExists => Conflict(new ProblemDetails
            {
                Title  = "Conflict",
                Detail = "A user with this email address already exists.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => StatusCode(StatusCodes.Status201Created, response)
        };
    }

    /// <summary>Authenticates a user and returns a token pair.</summary>
    /// <response code="200">Login successful.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var (result, response) = await _login.ExecuteAsync(request, ct);

        return result switch
        {
            LoginResult.InvalidCredentials => Unauthorized(new ProblemDetails
            {
                Title  = "Unauthorized",
                Detail = "Invalid email or password.",
                Status = StatusCodes.Status401Unauthorized
            }),
            _ => Ok(response)
        };
    }

    /// <summary>Issues a new token pair using a valid refresh token.</summary>
    /// <response code="200">Token refreshed.</response>
    /// <response code="401">Refresh token is invalid or expired.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var (result, response) = await _refresh.ExecuteAsync(request, ct);

        return result switch
        {
            RefreshResult.InvalidToken => Unauthorized(new ProblemDetails
            {
                Title  = "Unauthorized",
                Detail = "Refresh token is invalid or has expired.",
                Status = StatusCodes.Status401Unauthorized
            }),
            _ => Ok(response)
        };
    }

    /// <summary>Invalidates the current user's refresh token (logout).</summary>
    /// <response code="204">Logged out.</response>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        await _logout.ExecuteAsync(userId, ct);
        return NoContent();
    }
}
