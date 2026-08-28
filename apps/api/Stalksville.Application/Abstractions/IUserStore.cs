using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

public interface IUserStore
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

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

/// <summary>One audit row joined with the acting user's username (null when the actor is unknown).</summary>
public sealed record AuditEntryView(
    Guid Id,
    Guid? UserId,
    string? Username,
    string Action,
    string? Target,
    string? Details,
    DateTimeOffset OccurredAt);

/// <summary>Fire-and-forget audit trail for sensitive operations.</summary>
public interface IAuditLog
{
    Task WriteAsync(string action, string? target = null, object? details = null, CancellationToken cancellationToken = default);

    /// <summary>Paged audit query with optional filters (prefix match on action/target).</summary>
    Task<(int Total, IReadOnlyList<AuditEntryView> Entries)> QueryAsync(
        string? action = null,
        string? target = null,
        Guid? userId = null,
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default);
}
