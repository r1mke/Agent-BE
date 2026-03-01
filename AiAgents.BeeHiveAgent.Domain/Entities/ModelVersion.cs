namespace AiAgents.BeeHiveAgent.Domain.Entities
{
    public class ModelVersion
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public double Accuracy { get; set; }
    }
}
