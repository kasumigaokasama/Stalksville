using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Seeding;

/// <summary>
/// Applies migrations and seeds the initial admin user. The admin password comes from
/// configuration (Auth:AdminPassword); if unset, a random one is generated and logged once —
/// set Auth:AdminPassword explicitly (user-secrets or AUTH__ADMINPASSWORD env var) for anything
/// beyond a local demo.
/// </summary>
public sealed class StalksvilleSeeder(
    StalksvilleDbContext db,
    IUserStore users,
    IPasswordHasher hasher,
    IConfiguration configuration,
    ILogger<StalksvilleSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        if (await users.CountUsersAsync(cancellationToken) == 0)
        {
            const string username = "admin";
            var password = configuration["Auth:AdminPassword"];

            if (string.IsNullOrWhiteSpace(password))
            {
                password = $"stv-{Guid.NewGuid():N}";
                logger.LogWarning(
                    "No admin password configured (Auth:AdminPassword / AUTH__ADMINPASSWORD). " +
                    "Generated a random one — it will NOT be shown again. Username: {Username} Password: {Password}",
                    username, password);
            }

            await users.AddUserAsync(username, hasher.Hash(password), UserRole.Admin, cancellationToken);
            logger.LogInformation("Seeded admin user {Username} (role ADMIN)", username);
        }
    }
}
