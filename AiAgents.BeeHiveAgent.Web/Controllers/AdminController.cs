using AiAgents.BeeHiveAgent.Application.Interfaces;
using AiAgents.BeeHiveAgent.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.BeeHiveAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IReviewService _reviewService;

    public AdminController(IAppDbContext db, IReviewService reviewService)
    {
        _db = db;
        _reviewService = reviewService;
    }


    [HttpPost("trigger-retrain")]
    public async Task<IActionResult> TriggerRetrain()
    {
        var result = await _reviewService.TriggerRetrainAsync();
        if (!result.IsSuccess) return BadRequest(result.ErrorMessage);
        return Ok(result.Data);
    }

    [HttpPost("prove-learning")]
    public async Task<IActionResult> ProveLearning([FromServices] IBeeImageClassifier classifier, [FromServices] IModelTrainer trainer)
    {
        var testSample = await _db.ImageSamples.FirstOrDefaultAsync();
        if (testSample == null) return BadRequest("Nema slika u bazi za testiranje.");

        // 1. Predikcija prije učenja
        var predictionBefore = await classifier.PredictAsync(testSample.ImagePath);

        // Započinjemo transakciju kako bi baza ostala čista na kraju
        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // 2. Ubacujemo puno fake podataka da namjerno "iskrivimo" model
            var fakeData = new List<HiveImageSample>();
            for (int i = 0; i < 50; i++)
            {
                fakeData.Add(new HiveImageSample
                {
                    Id = Guid.NewGuid(),
                    ImagePath = testSample.ImagePath,
                    Label = "Pollen", // Namjerno forsiramo ovu labelu
                    Status = Domain.Enums.SampleStatus.Reviewed
                });
            }
            _db.ImageSamples.AddRange(fakeData);
            await _db.SaveChangesAsync();

            // 3. Pokrećemo učenje
            var allGold = await _db.ImageSamples.Where(s => s.Status == Domain.Enums.SampleStatus.Reviewed).ToListAsync();
            trainer.TrainModel(allGold);

            // 4. Predikcija poslije učenja (novi model)
            var predictionAfter = await classifier.PredictAsync(testSample.ImagePath);

            // 5. Očisti nered iz baze
            await transaction.RollbackAsync();

            return Ok(new
            {
                Message = "Proof of learning završen. Baza je netaknuta.",
                Image = testSample.ImagePath,
                ScoreBefore = predictionBefore,
                ScoreAfter = predictionAfter
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, $"Greška tokom dokazivanja: {ex.Message}");
        }
    }


    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var settings = await _db.Settings.FirstOrDefaultAsync();
        var totalSamples = await _db.ImageSamples.CountAsync();
        var goldSamples = await _db.ImageSamples
            .CountAsync(s => s.Status == Domain.Enums.SampleStatus.Reviewed);
        var pendingReview = await _db.ImageSamples
            .CountAsync(s => s.Status == Domain.Enums.SampleStatus.PendingReview);
        var queued = await _db.ImageSamples
            .CountAsync(s => s.Status == Domain.Enums.SampleStatus.Queued);

        var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "MLModels", "bee_model.zip");
        var modelExists = System.IO.File.Exists(modelPath);

        return Ok(new
        {
            ModelStatus = modelExists ? "✅ Ready" : "❌ Not trained",
            ModelPath = modelPath,
            Database = new
            {
                TotalSamples = totalSamples,
                GoldSamples = goldSamples,
                PendingReview = pendingReview,
                Queued = queued
            },
            Training = new
            {
                NewGoldSinceLastTrain = settings?.NewGoldSinceLastTrain ?? 0,
                RetrainThreshold = settings?.RetrainGoldThreshold ?? 50,
                WillTrainOnNextCycle = (settings?.NewGoldSinceLastTrain ?? 0) >= (settings?.RetrainGoldThreshold ?? 50)
            }
        });
    }


    [HttpDelete("reset-database")]
    public async Task<IActionResult> ResetDatabase()
    {

        var predictions = await _db.Predictions.ToListAsync();
        _db.Predictions.RemoveRange(predictions);


        var samples = await _db.ImageSamples.ToListAsync();
        _db.ImageSamples.RemoveRange(samples);


        var settings = await _db.Settings.FirstOrDefaultAsync();
        if (settings != null)
        {
            settings.NewGoldSinceLastTrain = 0;
        }

        await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(new
        {
            Message = "Database reset complete!",
            DeletedPredictions = predictions.Count,
            DeletedSamples = samples.Count
        });
    }
}