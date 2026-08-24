using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class UserStore(StalksvilleDbContext db) : IUserStore
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), cancellationToken);

    public async Task<User> AddUserAsync(string username, string passwordHash, UserRole role, CancellationToken cancellationToken = default)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = passwordHash,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public Task<int> CountUsersAsync(CancellationToken cancellationToken = default)
        => db.Users.CountAsync(cancellationToken);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default)
        => await db.Users.OrderBy(u => u.Username).ToListAsync(cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
        => db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower(), cancellationToken);

    public async Task UpdateLastLoginAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        user.LastLoginAt = at;
        await db.SaveChangesAsync(cancellationToken);
    }
}
