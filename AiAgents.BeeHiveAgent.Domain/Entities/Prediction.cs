using AiAgents.BeeHiveAgent.Domain.Enums;
using AiAgents.Core.Abstractions;

namespace AiAgents.BeeHiveAgent.Domain.Entities;

public class Prediction : IAction
{
    public Guid Id { get; set; }
    public Guid SampleId { get; set; }
    public string ModelVersion { get; set; } = string.Empty;


    public float Score { get; set; }

    public string PredictedLabel { get; set; } = string.Empty;

    public Decision Decision { get; set; }
    public DateTime CreatedAt { get; set; }
}