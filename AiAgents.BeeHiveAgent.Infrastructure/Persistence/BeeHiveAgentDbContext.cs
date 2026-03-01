using AiAgents.BeeHiveAgent.Application.Interfaces;
using AiAgents.BeeHiveAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AiAgents.BeeHiveAgent.Infrastructure.Persistence;

public class BeeHiveAgentDbContext : DbContext, IAppDbContext
{
    public BeeHiveAgentDbContext(DbContextOptions<BeeHiveAgentDbContext> options)
        : base(options)
    {
    }

    public DbSet<HiveImageSample> ImageSamples { get; set; }
    public DbSet<Prediction> Predictions { get; set; }
    public DbSet<SystemSettings> Settings { get; set; }
    public DbSet<ModelVersion> ModelVersions { get; set; }

    public override DatabaseFacade Database => base.Database;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<HiveImageSample>()
            .HasMany(s => s.Predictions)
            .WithOne()
            .HasForeignKey(p => p.SampleId);

        modelBuilder.Entity<SystemSettings>().HasData(
            new SystemSettings
            {
                Id = 1,
                NewGoldSinceLastTrain = 0,
                IsRetrainEnabled = true,
                RetrainGoldThreshold = 50,
                AutoThresholdHigh = 0.90f,
                AutoThresholdLow = 0.15f,
                ActiveModelVersionId = null
            }
        );

        modelBuilder.Entity<Prediction>()
            .Property(p => p.Score)
            .HasColumnType("real");
    }
}