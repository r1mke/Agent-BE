using AiAgents.BeeHiveAgent.Application.Interfaces;
using AiAgents.BeeHiveAgent.Domain.Entities;
using AiAgents.BeeHiveAgent.Domain.Enums;
using AiAgents.BeeHiveAgent.Web.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.BeeHiveAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SamplesController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public SamplesController(IAppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }


    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] UploadImageDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return BadRequest("Please upload an image file.");

        var uploadFolder = Path.Combine(_env.ContentRootPath, "UserUploads");
        Directory.CreateDirectory(uploadFolder);

        var fileName = $"{Guid.NewGuid()}_{dto.File.FileName}";
        var fullPath = Path.Combine(uploadFolder, fileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await dto.File.CopyToAsync(stream);
        }

        var sample = new HiveImageSample
        {
            Id = Guid.NewGuid(),
            HiveId = Guid.NewGuid(),
            ImagePath = fullPath,
            CapturedAt = DateTime.UtcNow,
            Status = SampleStatus.Queued,
            TaskType = dto.TaskType
        };

        _db.ImageSamples.Add(sample);
        await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(new { Message = "Image queued for analysis", SampleId = sample.Id });
    }


    [HttpGet("results")]
    public async Task<IActionResult> GetResults()
    {
        var results = await _db.ImageSamples
            .Include(s => s.Predictions)
            .Where(s => s.ImagePath.Contains("UserUploads"))
            .OrderByDescending(s => s.CapturedAt)
            .Take(10)
            .Select(s => new
            {
                s.Id,
                s.Status,
                s.Label,
                s.ImagePath,
                Predictions = s.Predictions.Select(p => new { p.Score, p.PredictedLabel, p.Decision })
            })
            .ToListAsync(CancellationToken.None);

        return Ok(results);
    }


    [HttpPost("results-by-ids")]
    public async Task<IActionResult> GetResultsByIds([FromBody] IdsRequestDto request)
    {
        if (request.Ids == null || request.Ids.Count == 0)
            return BadRequest("No IDs provided.");

        var guids = request.Ids.Select(id => Guid.Parse(id)).ToList();

        var results = await _db.ImageSamples
            .Include(s => s.Predictions)
            .Where(s => guids.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                s.Status,
                s.Label,
                s.ImagePath,
                Predictions = s.Predictions
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new
                    {
                        p.Score,
                        p.PredictedLabel,
                        Decision = p.Decision.ToString()
                    })
                    .ToList()
            })
            .ToListAsync(CancellationToken.None);

        return Ok(results);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetUploadStats()
    {
        var today = DateTime.UtcNow.Date;

        var todayUploads = await _db.ImageSamples
            .CountAsync(s => s.ImagePath.Contains("UserUploads") && s.CapturedAt >= today);

        var pendingReview = await _db.ImageSamples
            .CountAsync(s => s.ImagePath.Contains("UserUploads") && s.Status == SampleStatus.PendingReview);

        var reviewed = await _db.ImageSamples
            .CountAsync(s => s.ImagePath.Contains("UserUploads") && s.Status == SampleStatus.Reviewed);

        return Ok(new
        {
            TodayUploads = todayUploads,
            PendingReview = pendingReview,
            Reviewed = reviewed
        });
    }


    [HttpGet("pending-review")]
    public async Task<IActionResult> GetPendingReview()
    {
        var pending = await _db.ImageSamples
            .Include(s => s.Predictions)
            .Where(s => s.ImagePath.Contains("UserUploads"))
            .Where(s => s.Status == SampleStatus.PendingReview)
            .OrderByDescending(s => s.CapturedAt)
            .Select(s => new
            {
                s.Id,
                s.Status,
                s.ImagePath,
                FileName = Path.GetFileName(s.ImagePath),
                Prediction = s.Predictions
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new { p.PredictedLabel, p.Score, p.Decision })
                    .FirstOrDefault()
            })
            .ToListAsync(CancellationToken.None);

        return Ok(new
        {
            Count = pending.Count,
            Samples = pending
        });
    }


    [HttpPost("review")]
    public async Task<IActionResult> ReviewSample([FromBody] ReviewRequestDto request, [FromServices] IReviewService reviewService)
    {
        var result = await reviewService.SubmitReviewAsync(request);
        if (!result.IsSuccess) return NotFound(result.ErrorMessage);
        return Ok(result.Data);
    }

    [HttpPost("review-bulk")]
    public async Task<IActionResult> ReviewBulk([FromBody] List<ReviewRequestDto> requests, [FromServices] IReviewService reviewService)
    {
        var result = await reviewService.SubmitBulkReviewAsync(requests);
        if (!result.IsSuccess) return BadRequest(result.ErrorMessage);
        return Ok(result.Data);
    }
}


public class IdsRequestDto
{
    public List<string> Ids { get; set; } = new();
}