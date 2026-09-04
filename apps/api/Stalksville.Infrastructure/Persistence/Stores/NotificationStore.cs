using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class NotificationStore(StalksvilleDbContext db) : INotificationStore
{
    public async Task<IReadOnlyList<NotificationChannel>> ListAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        var query = db.NotificationChannels.AsNoTracking().AsQueryable();
        if (enabledOnly)
        {
            query = query.Where(c => c.Enabled);
        }

        return await query.OrderBy(c => c.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<NotificationChannel?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        => db.NotificationChannels.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task AddAsync(NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        db.NotificationChannels.Add(channel);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var removed = await db.NotificationChannels
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return removed == 1;
    }

    public async Task<bool> UpdateAsync(NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        var updated = await db.NotificationChannels
            .Where(c => c.Id == channel.Id)
            .ExecuteUpdateAsync(set => set
                .SetProperty(c => c.Name, channel.Name)
                .SetProperty(c => c.TargetUrl, channel.TargetUrl)
                .SetProperty(c => c.Enabled, channel.Enabled)
                .SetProperty(c => c.LastDeliveryAt, channel.LastDeliveryAt)
                .SetProperty(c => c.LastDeliveryStatus, channel.LastDeliveryStatus), cancellationToken);
        return updated == 1;
    }
}
