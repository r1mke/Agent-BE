using AiAgents.BeeHiveAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AiAgents.BeeHiveAgent.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<HiveImageSample> ImageSamples { get; set; }
    DbSet<Prediction> Predictions { get; set; }
    DbSet<SystemSettings> Settings { get; set; }
    DbSet<ModelVersion> ModelVersions { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    DatabaseFacade Database { get; }
}