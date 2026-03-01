using AiAgents.BeeHiveAgent.Application.Interfaces;
using AiAgents.BeeHiveAgent.Domain.Entities;
using AiAgents.BeeHiveAgent.Domain.Enums;
using AiAgents.Core.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.BeeHiveAgent.Application.Runners;

public record RetrainResult(string NewModelVersion, int TrainingCount) : IResult;

public class RetrainAgentRunner : SoftwareAgent<SystemSettings, Prediction, RetrainResult>
{
    private readonly IAppDbContext _db;
    private readonly IModelTrainer _trainer;

    public RetrainAgentRunner(IAppDbContext db, IModelTrainer trainer)
    {
        _db = db;
        _trainer = trainer;
    }

    public override async Task<RetrainResult?> StepAsync(CancellationToken ct)
    {

        var settings = await _db.Settings.FirstOrDefaultAsync(ct);

        if (settings == null || !settings.IsRetrainEnabled)
        {
            return null;
        }


        if (settings.NewGoldSinceLastTrain < settings.RetrainGoldThreshold)
        {
            return null;
        }

        Console.WriteLine($"🎓 RETRAIN AGENT: Pokrećem trening! Novih gold podataka: {settings.NewGoldSinceLastTrain}");


        var goldSamples = await _db.ImageSamples
            .Where(s => s.Status == SampleStatus.Reviewed && !string.IsNullOrEmpty(s.Label))
            .ToListAsync(ct);

        if (goldSamples.Count == 0)
        {
            Console.WriteLine("⚠️ RETRAIN AGENT: Nema gold samplea sa labelama!");
            return null;
        }


        var newModelVersion = _trainer.TrainModel(goldSamples);


        if (newModelVersion == "SKIPPED_BAD_DATA" || newModelVersion == "TRAINING_FAILED")
        {
            Console.WriteLine($"⚠️ RETRAIN AGENT: Trening neuspješan ({newModelVersion}). Čekam više podataka.");
            return null;
        }


        settings.NewGoldSinceLastTrain = 0;


        await _db.SaveChangesAsync(ct);

        Console.WriteLine($"✅ RETRAIN AGENT: Trening završen! Nova aktivna verzija: {newModelVersion}");

        return new RetrainResult(newModelVersion, goldSamples.Count);
    }
}