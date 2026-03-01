using AiAgents.BeeHiveAgent.Application.Interfaces;
using AiAgents.BeeHiveAgent.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;

namespace AiAgents.BeeHiveAgent.Infrastructure.ML;

public class TrainingService : IModelTrainer
{
    private readonly string _modelsFolder;
    private readonly MLContext _mlContext;
    private readonly IServiceScopeFactory _scopeFactory;

    // Zadržavamo event da bismo javili Classifier-u da reload-a model
    public static event Action? OnModelTrained;

    public TrainingService(IServiceScopeFactory scopeFactory)
    {
        _mlContext = new MLContext(seed: 42);
        _scopeFactory = scopeFactory;

        _modelsFolder = Path.Combine(Directory.GetCurrentDirectory(), "MLModels");

        if (!Directory.Exists(_modelsFolder))
            Directory.CreateDirectory(_modelsFolder);
    }

    public bool ModelExists()
    {
        // Provjerava da li postoji barem jedan model u bazi
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return db.Settings.Any(s => s.ActiveModelVersionId != null);
    }

    public string TrainModel(List<HiveImageSample> goldSamples)
    {
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("🐝 POKREĆEM TRENING ML MODELA (PRO VERZIJA)");
        Console.WriteLine("═══════════════════════════════════════════════════");

        var countPollen = goldSamples.Count(s => s.Label == "Pollen");
        var countNoPollen = goldSamples.Count(s => s.Label == "NoPollen");

        Console.WriteLine($"📊 Dataset statistika: Pollen ({countPollen}), NoPollen ({countNoPollen})");

        if (countPollen == 0 || countNoPollen == 0)
        {
            Console.WriteLine("❌ GREŠKA: Fali jedna od klasa! Trening nije moguć.");
            return "SKIPPED_BAD_DATA";
        }

        var validSamples = goldSamples.Where(s => File.Exists(s.ImagePath)).ToList();
        if (validSamples.Count < 10)
        {
            Console.WriteLine("❌ GREŠKA: Premalo validnih slika za trening (min. 10)!");
            return "SKIPPED_BAD_DATA";
        }

        var trainData = validSamples.Select(s => new ModelInput
        {
            ImagePath = s.ImagePath,
            Label = s.Label ?? "NoPollen"
        }).ToList();

        var trainingDataView = _mlContext.Data.LoadFromEnumerable(trainData);

        var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(
                inputColumnName: "Label",
                outputColumnName: "LabelKey")
            .Append(_mlContext.Transforms.LoadRawImageBytes(
                outputColumnName: "Image",
                imageFolder: null,
                inputColumnName: "ImagePath"))
            .Append(_mlContext.MulticlassClassification.Trainers.ImageClassification(
                featureColumnName: "Image",
                labelColumnName: "LabelKey"))
            .Append(_mlContext.Transforms.Conversion.MapKeyToValue(
                outputColumnName: "PredictedLabel",
                inputColumnName: "PredictedLabel"));

        Console.WriteLine("💪 Započinjem trening modela (ovo može potrajati)...");
        ITransformer trainedModel;
        try
        {
            trainedModel = pipeline.Fit(trainingDataView);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Greška tokom treninga: {ex.Message}");
            return "TRAINING_FAILED";
        }

        // --- MLOps EVALUACIJA MODELA ---
        Console.WriteLine("📈 Evaluacija modela...");
        var predictions = trainedModel.Transform(trainingDataView);
        var metrics = _mlContext.MulticlassClassification.Evaluate(predictions, labelColumnName: "LabelKey");
        double accuracy = metrics.MicroAccuracy;
        Console.WriteLine($"📊 Preciznost novog modela (MicroAccuracy): {accuracy:P2}");

        // --- VERZIONIRANJE I SPAŠAVANJE ---
        var newVersionId = Guid.NewGuid();
        var modelFileName = $"bee_model_{newVersionId}.zip";
        var newModelPath = Path.Combine(_modelsFolder, modelFileName);

        try
        {
            _mlContext.Model.Save(trainedModel, trainingDataView.Schema, newModelPath);
            Console.WriteLine($"✅ Model uspješno spašen na: {newModelPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Greška pri spašavanju na disk: {ex.Message}");
            return "SAVE_FAILED";
        }

        // --- UPIS U BAZU ---
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            var novaVerzija = new ModelVersion
            {
                Id = newVersionId,
                CreatedAt = DateTime.UtcNow,
                FilePath = newModelPath,
                Accuracy = accuracy
            };
            db.ModelVersions.Add(novaVerzija);

            var settings = db.Settings.FirstOrDefault();
            if (settings != null)
            {
                var aktivniModel = settings.ActiveModelVersionId.HasValue ? db.ModelVersions.Find(settings.ActiveModelVersionId.Value) : null;

                // Promijeni aktivni model samo ako je novi bolji ili ako aktivni ne postoji
                if (aktivniModel == null || novaVerzija.Accuracy >= aktivniModel.Accuracy)
                {
                    settings.ActiveModelVersionId = novaVerzija.Id;
                    Console.WriteLine("🏆 NOVI MODEL JE POSTAO AKTIVAN (Bolji ili prvi model)!");
                }
                else
                {
                    Console.WriteLine("⚠️ Novi model ima lošiju preciznost od postojećeg. Sačuvan je u bazu, ali nije aktiviran.");
                }

                settings.NewGoldSinceLastTrain = 0; // Resetujemo counter
            }
            db.SaveChangesAsync().Wait();
        }

        OnModelTrained?.Invoke();

        return newVersionId.ToString();
    }
}