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
    IApiKeyStore apiKeys,
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

    // ---- client API keys (expansion phase 5) ----

    public sealed record CreateApiKeyRequest(string Name);
    public sealed record ApiKeyDto(Guid Id, string Name, string Prefix, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt);
    public sealed record CreatedApiKeyDto(ApiKeyDto Key, string ApiKey);

    /// <summary>Creates a client API key. The full key is returned exactly once — only its hash is stored.</summary>
    [HttpPost("{userId:guid}/api-keys")]
    [ProducesResponseType<CreatedApiKeyDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateApiKey(Guid userId, [FromBody] CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound(new ProblemDetails { Title = "User not found." });
        }

        var name = request.Name.Trim();
        if (name.Length is < 1 or > 64)
        {
            return BadRequest(new ProblemDetails { Title = "Key name must be 1-64 characters." });
        }

        var (key, prefix) = ApiKeyAuthHandler.Generate();
        var created = await apiKeys.CreateAsync(new Domain.Entities.ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Prefix = prefix,
            KeyHash = ApiKeyAuthHandler.Hash(key),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await audit.WriteAsync("API_KEY_CREATED", $"user:{user.Username}", new { created.Name, created.Prefix }, cancellationToken);

        return CreatedAtAction(nameof(ListApiKeys), new { userId },
            new CreatedApiKeyDto(ToDto(created), key));
    }

    [HttpGet("{userId:guid}/api-keys")]
    [ProducesResponseType<IReadOnlyList<ApiKeyDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListApiKeys(Guid userId, CancellationToken cancellationToken)
    {
        var list = await apiKeys.ListForUserAsync(userId, cancellationToken);
        return Ok(list.Select(ToDto).ToList());
    }

    /// <summary>Soft-revokes a key; requests using it fail with 401 immediately.</summary>
    [HttpDelete("{userId:guid}/api-keys/{keyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeApiKey(Guid userId, Guid keyId, CancellationToken cancellationToken)
    {
        if (await apiKeys.RevokeAsync(keyId, cancellationToken))
        {
            await audit.WriteAsync("API_KEY_REVOKED", $"apiKey:{keyId}", null, cancellationToken);
            return NoContent();
        }

        return NotFound();
    }

    private static ApiKeyDto ToDto(Domain.Entities.ApiKey key) => new(
        key.Id, key.Name, key.Prefix, key.CreatedAt, key.LastUsedAt, key.RevokedAt);
}
