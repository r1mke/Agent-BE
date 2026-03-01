using AiAgents.BeeHiveAgent.Application.Interfaces;
using AiAgents.BeeHiveAgent.Application.Services;
using AiAgents.BeeHiveAgent.Domain.Entities;
using AiAgents.BeeHiveAgent.Domain.Enums;
using AiAgents.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AiAgents.BeeHiveAgent.Application.Runners;

public record ScoringTickResult(Guid SampleId, string OldStatus, string NewStatus, float Score, string Decision) : IResult;

public class ScoringAgentRunner : SoftwareAgent<HiveImageSample, Prediction, ScoringTickResult>
{
    private readonly IAppDbContext _db;
    private readonly IBeeImageClassifier _classifier;
    private readonly ScoringPolicy _policy;

    public ScoringAgentRunner(IAppDbContext db, IBeeImageClassifier classifier, ScoringPolicy policy)
    {
        _db = db;
        _classifier = classifier;
        _policy = policy;
    }

    public override async Task<ScoringTickResult?> StepAsync(CancellationToken ct)
    {
        HiveImageSample? sample = null;


        using (var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            try
            {
                sample = await _db.ImageSamples
                    .Where(s => s.Status == SampleStatus.Queued)
                    .OrderBy(s => s.CapturedAt)
                    .FirstOrDefaultAsync(ct);

                if (sample != null)
                {

                    sample.MarkProcessing();
                    await _db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                }
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        if (sample == null) return null;


        var predictions = await _classifier.PredictAsync(sample.ImagePath);

        var bestMatch = predictions.OrderByDescending(k => k.Value).First();
        var score = bestMatch.Value;
        var label = bestMatch.Key;

        var usedModelVersion = _classifier.ModelVersion;

        var settings = await _db.Settings.FirstOrDefaultAsync(ct)
                       ?? new SystemSettings();


        var decisionResult = _policy.Evaluate(score, label, settings);

        var prediction = new Prediction
        {
            Id = Guid.NewGuid(),
            SampleId = sample.Id,
            Score = score,
            PredictedLabel = label,
            Decision = decisionResult.Decision,
            CreatedAt = DateTime.UtcNow,
            ModelVersion = usedModelVersion
        };

        Console.WriteLine($"🤖 AGENT: Obradio {sample.Id} | Model: {usedModelVersion} | Score: {score:P2}");

        _db.Predictions.Add(prediction);
        sample.Status = decisionResult.NewStatus;

        await _db.SaveChangesAsync(ct);

        return new ScoringTickResult(sample.Id, "Queued", sample.Status.ToString(), score, decisionResult.Decision.ToString());
    }
}