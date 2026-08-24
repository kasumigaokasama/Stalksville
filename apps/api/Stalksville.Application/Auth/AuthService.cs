using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Domain.Entities;

namespace Stalksville.Application.Auth;

public sealed class AuthService(
    IUserStore users,
    IPasswordHasher hasher,
    IAuditLog audit)
{
    /// <summary>Validates credentials; returns null on failure (with a failed-login audit entry).</summary>
    public async Task<UserDto?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByUsernameAsync(username, cancellationToken);

        if (user is null || !hasher.Check(user.PasswordHash, password))
        {
            await audit.WriteAsync(AuditActions.UserLoginFailed, $"user:{username}", cancellationToken: cancellationToken);
            return null;
        }

        await users.UpdateLastLoginAsync(user.Id, DateTimeOffset.UtcNow, cancellationToken);
        await audit.WriteAsync(AuditActions.UserLogin, $"user:{username}", cancellationToken: cancellationToken);

        return new UserDto(user.Id, user.Username, user.Role.ToString().ToUpperInvariant());
    }
}
