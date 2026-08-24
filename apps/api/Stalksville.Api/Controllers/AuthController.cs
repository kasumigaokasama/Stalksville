using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stalksville.Application.Auth;
using Stalksville.Application.Models;
using Stalksville.Api.Security;

namespace Stalksville.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(AuthService auth, ITokenService tokens) : ControllerBase
{
    /// <summary>Exchanges credentials for a JWT. The only anonymous endpoint.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await auth.LoginAsync(request.Username, request.Password, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Login failed",
                Detail = "Invalid username or password."
            });
        }

        return Ok(tokens.CreateToken(user));
    }

    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public IActionResult Me()
    {
        return Ok(new UserDto(
            Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty,
            User.Identity?.Name ?? User.FindFirst("name")?.Value ?? "?",
            User.FindFirst("role")?.Value ?? "?"));
    }
}
