using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

/// <summary>Persistence port for persisted exposure assessments (derived, evidence-linked).</summary>
public interface IExposureStore
{
    Task AddAsync(ExposureAssessment assessment, CancellationToken cancellationToken = default);

    /// <summary>The newest assessment; null before the first one.</summary>
    Task<ExposureAssessment?> GetLatestAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>The assessment before the newest one; null when fewer than two exist.</summary>
    Task<ExposureAssessment?> GetPreviousAsync(Guid playerId, CancellationToken cancellationToken = default);
}
