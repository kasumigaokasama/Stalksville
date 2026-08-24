using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stalksville.Api.Security;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;

namespace Stalksville.Api.Controllers;

public sealed record CreateUserRequest(string Username, string Password, string Role);

public sealed record AdminUserDto(Guid Id, string Username, string Role, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt);

/// <summary>User management — ADMIN only (plan §32). Users are created, never deleted.</summary>
[ApiController]
[Authorize(Policy = Policies.Admin)]
[Route("api/v1/admin/users")]
public sealed class UsersController(
    IUserStore users,
    IPasswordHasher hasher,
    IAuditLog audit) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var list = await users.ListAsync(cancellationToken);
        return Ok(list.Select(u => new AdminUserDto(u.Id, u.Username, u.Role.ToString().ToUpperInvariant(), u.CreatedAt, u.LastLoginAt)).ToList());
    }

    [HttpPost]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        if (username.Length is < 3 or > 64)
        {
            return BadRequest(new ProblemDetails { Title = "Username must be 3-64 characters." });
        }

        if (request.Password.Length < 8)
        {
            return BadRequest(new ProblemDetails { Title = "Password must be at least 8 characters." });
        }

        if (!Enum.TryParse<UserRole>(request.Role.Trim(), ignoreCase: true, out var role)
            || !Enum.IsDefined(role))
        {
            return BadRequest(new ProblemDetails { Title = "Role must be ADMIN, ANALYST or VIEWER." });
        }

        if (await users.UsernameExistsAsync(username, cancellationToken))
        {
            return Conflict(new ProblemDetails { Title = $"Username '{username}' already exists." });
        }

        var user = await users.AddUserAsync(username, hasher.Hash(request.Password), role, cancellationToken);
        await audit.WriteAsync("USER_CREATED", $"user:{username}", new { role }, cancellationToken);

        return CreatedAtAction(nameof(List), new AdminUserDto(user.Id, user.Username, user.Role.ToString().ToUpperInvariant(), user.CreatedAt, user.LastLoginAt));
    }
}
