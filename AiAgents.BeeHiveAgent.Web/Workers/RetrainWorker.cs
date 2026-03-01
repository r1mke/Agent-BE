using AiAgents.BeeHiveAgent.Application.Runners;

namespace AiAgents.BeeHiveAgent.Web.Workers;


public class RetrainWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RetrainWorker> _logger;

    public RetrainWorker(IServiceProvider serviceProvider, ILogger<RetrainWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("═══════════════════════════════════════════════════");
        _logger.LogInformation("🎓 RETRAIN WORKER: Pokrenut!");
        _logger.LogInformation("═══════════════════════════════════════════════════");


        await Task.Delay(5000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var runner = scope.ServiceProvider.GetRequiredService<RetrainAgentRunner>();

                    _logger.LogDebug("🔍 RETRAIN WORKER: Provjeravam da li ima posla...");


                    var result = await runner.StepAsync(stoppingToken);

                    if (result != null)
                    {

                        _logger.LogInformation("═══════════════════════════════════════════════════");
                        _logger.LogInformation($"🎉 RETRAIN COMPLETE!");
                        _logger.LogInformation($"   -> Nova verzija: {result.NewModelVersion}");
                        _logger.LogInformation($"   -> Trenirano na: {result.TrainingCount} slika");
                        _logger.LogInformation("═══════════════════════════════════════════════════");


                        await Task.Delay(60000, stoppingToken);
                    }
                    else
                    {

                        await Task.Delay(10000, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {

                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ RETRAIN WORKER: Greška u petlji!");


                await Task.Delay(30000, stoppingToken);
            }
        }

        _logger.LogInformation("🛑 RETRAIN WORKER: Zaustavljen.");
    }
}