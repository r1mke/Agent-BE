namespace AiAgents.BeeHiveAgent.Application.Interfaces;

public interface IBeeImageClassifier
{
    string ModelVersion { get; }
    Task<Dictionary<string, float>> PredictAsync(string imagePath);
}
