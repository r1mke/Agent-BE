namespace AiAgents.BeeHiveAgent.Application.Interfaces
{
    public interface IReviewService
    {
        Task<(bool IsSuccess, string? ErrorMessage, object? Data)> SubmitReviewAsync(ReviewRequestDto request);
        Task<(bool IsSuccess, string? ErrorMessage, object? Data)> SubmitBulkReviewAsync(List<ReviewRequestDto> requests);
        Task<(bool IsSuccess, string? ErrorMessage, object? Data)> TriggerRetrainAsync();
    }
}
