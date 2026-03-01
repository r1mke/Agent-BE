using AiAgents.BeeHiveAgent.Domain.Entities;

namespace AiAgents.BeeHiveAgent.Application.Interfaces;

public interface IModelTrainer
{

    string TrainModel(List<HiveImageSample> goldSamples);


    bool ModelExists();
}