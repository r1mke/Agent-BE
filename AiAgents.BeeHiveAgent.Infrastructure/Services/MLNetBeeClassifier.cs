using AiAgents.BeeHiveAgent.Application.Interfaces;
using AiAgents.BeeHiveAgent.Infrastructure.ML;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;

namespace AiAgents.BeeHiveAgent.Infrastructure.Services;

public class MLNetBeeClassifier : IBeeImageClassifier, IDisposable
{
    private readonly MLContext _mlContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private ITransformer? _trainedModel;
    private PredictionEngine<ModelInput, ModelOutput>? _predictionEngine;
    private readonly object _lock = new object();
    private bool _disposed = false;
    private Guid? _currentLoadedVersionId = null;

    public MLNetBeeClassifier(IServiceScopeFactory scopeFactory)
    {
        _mlContext = new MLContext();
        _scopeFactory = scopeFactory;

        TrainingService.OnModelTrained += ReloadModel;

        LoadModel();
    }

    private void LoadModel()
    {
        lock (_lock)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            var settings = db.Settings.FirstOrDefault();

            if (settings == null || settings.ActiveModelVersionId == null)
            {
                Console.WriteLine($"⚠️ MLNetBeeClassifier: Nema aktivne verzije modela u bazi.");
                Console.WriteLine("   -> Čekam da RetrainWorker završi prvi trening...");
                _predictionEngine = null;
                return;
            }

            // Ako je već učitan trenutno aktivni model, ne moramo ga ponovo učitavati
            if (_currentLoadedVersionId == settings.ActiveModelVersionId)
            {
                return;
            }

            var activeModel = db.ModelVersions.Find(settings.ActiveModelVersionId.Value);

            if (activeModel == null || !File.Exists(activeModel.FilePath))
            {
                Console.WriteLine($"❌ MLNetBeeClassifier: Aktivan model (ID: {settings.ActiveModelVersionId}) ne postoji na disku!");
                _predictionEngine = null;
                return;
            }

            try
            {
                Console.WriteLine($"📂 MLNetBeeClassifier: Učitavam verziju {activeModel.Id} iz {activeModel.FilePath}");

                DataViewSchema modelSchema;
                _trainedModel = _mlContext.Model.Load(activeModel.FilePath, out modelSchema);
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(_trainedModel);
                _currentLoadedVersionId = activeModel.Id;

                Console.WriteLine("✅ MLNetBeeClassifier: Model uspješno učitan i spreman za predikcije!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MLNetBeeClassifier: Greška pri učitavanju modela: {ex.Message}");
                _predictionEngine = null;
            }
        }
    }

    private void ReloadModel()
    {
        Console.WriteLine("🔄 MLNetBeeClassifier: Primljena obavijest o novom modelu, provjeravam aktivnu verziju...");
        LoadModel();
    }

    public Task<Dictionary<string, float>> PredictAsync(string imagePath)
    {
        var result = new Dictionary<string, float>();

        if (_predictionEngine == null)
        {
            LoadModel(); // Pokušaj opet učitati

            if (_predictionEngine == null)
            {
                Console.WriteLine($"⚠️ PREDIKCIJA PRESKOČENA: Aktivni model nije pronađen!");
                result.Add("Unknown", 0.0f);
                return Task.FromResult(result);
            }
        }

        if (!File.Exists(imagePath))
        {
            Console.WriteLine($"❌ PREDIKCIJA GREŠKA: Slika ne postoji: {imagePath}");
            result.Add("Error_FileNotFound", 0.0f);
            return Task.FromResult(result);
        }

        try
        {
            var input = new ModelInput { ImagePath = imagePath };

            ModelOutput prediction;
            lock (_lock)
            {
                prediction = _predictionEngine.Predict(input);
            }

            if (prediction.Score != null && prediction.Score.Length > 0)
            {
                float maxScore = prediction.Score.Max();
                string predictedLabel = prediction.PredictedLabel ?? "Unknown";

                result.Add(predictedLabel, maxScore);

                Console.WriteLine($"🧠 PREDIKCIJA: {Path.GetFileName(imagePath)} -> Label: {predictedLabel} (Confidence: {maxScore:P1})");
            }
            else
            {
                result.Add("Unknown", 0.0f);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ PREDIKCIJA GREŠKA: {ex.Message}");
            result.Add("Error", 0.0f);
        }

        return Task.FromResult(result);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            TrainingService.OnModelTrained -= ReloadModel;
            _predictionEngine?.Dispose();
            _disposed = true;
        }
    }
}