using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class ExposureStore(StalksvilleDbContext db) : IExposureStore
{
    public async Task AddAsync(ExposureAssessment assessment, CancellationToken cancellationToken = default)
    {
        db.ExposureAssessments.Add(assessment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<ExposureAssessment?> GetLatestAsync(Guid playerId, CancellationToken cancellationToken = default)
        => db.ExposureAssessments.AsNoTracking()
            .Where(a => a.PlayerId == playerId)
            .OrderByDescending(a => a.AssessedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ExposureAssessment?> GetPreviousAsync(Guid playerId, CancellationToken cancellationToken = default)
        => await db.ExposureAssessments.AsNoTracking()
            .Where(a => a.PlayerId == playerId)
            .OrderByDescending(a => a.AssessedAt)
            .Skip(1)
            .FirstOrDefaultAsync(cancellationToken);
}
