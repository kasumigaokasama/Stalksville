using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

public interface IUserStore
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<User> AddUserAsync(string username, string passwordHash, UserRole role, CancellationToken cancellationToken = default);

    Task<int> CountUsersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);

    Task UpdateLastLoginAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);
}

/// <summary>Hashing port so the Application layer stays free of any specific identity stack.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Check(string passwordHash, string password);
}

/// <summary>Fire-and-forget audit trail for sensitive operations.</summary>
public interface IAuditLog
{
    Task WriteAsync(string action, string? target = null, object? details = null, CancellationToken cancellationToken = default);
}
