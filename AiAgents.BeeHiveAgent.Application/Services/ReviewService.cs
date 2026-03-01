using AiAgents.BeeHiveAgent.Application.Interfaces;
using AiAgents.BeeHiveAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.BeeHiveAgent.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IAppDbContext _db;

    public ReviewService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<(bool IsSuccess, string? ErrorMessage, object? Data)> SubmitReviewAsync(ReviewRequestDto request)
    {
        var sample = await _db.ImageSamples.FindAsync(request.SampleId);
        if (sample == null) return (false, "Sample ID not found.", null);

        sample.Label = request.IsPollen ? "Pollen" : "NoPollen";
        sample.Status = SampleStatus.Reviewed;

        var settings = await _db.Settings.FirstOrDefaultAsync();
        if (settings != null) settings.NewGoldSinceLastTrain++;

        await _db.SaveChangesAsync();

        return (true, null, new { Message = "Review saved!", SampleId = sample.Id, AssignedLabel = sample.Label });
    }

    public async Task<(bool IsSuccess, string? ErrorMessage, object? Data)> SubmitBulkReviewAsync(List<ReviewRequestDto> requests)
    {
        if (requests == null || !requests.Any()) return (false, "No reviews provided.", null);

        var settings = await _db.Settings.FirstOrDefaultAsync();
        int count = 0;

        foreach (var req in requests)
        {
            var sample = await _db.ImageSamples.FindAsync(req.SampleId);
            if (sample == null) continue;

            sample.Label = req.IsPollen ? "Pollen" : "NoPollen";
            sample.Status = SampleStatus.Reviewed;
            if (settings != null) settings.NewGoldSinceLastTrain++;
            count++;
        }

        await _db.SaveChangesAsync();
        return (true, null, new { Message = $"Reviewed {count} samples!" });
    }

    public async Task<(bool IsSuccess, string? ErrorMessage, object? Data)> TriggerRetrainAsync()
    {
        var settings = await _db.Settings.FirstOrDefaultAsync();
        if (settings == null) return (false, "Settings not found.", null);

        var goldCount = await _db.ImageSamples.CountAsync(s => s.Status == SampleStatus.Reviewed);
        if (goldCount == 0) return (false, "No gold samples to train on.", null);

        settings.NewGoldSinceLastTrain = goldCount;
        await _db.SaveChangesAsync();

        return (true, null, new { Message = "Retrain triggered!", GoldSamples = goldCount });
    }
}