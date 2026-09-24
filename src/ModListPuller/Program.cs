using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeadlyScraper;
using ModListPuller;

var sources = new[]
{
    (Game: "k1", Url: "https://raw.githubusercontent.com/KOTOR-Community-Portal/mod-builds/dev/content/k1/full.md"),
    (Game: "k2", Url: "https://raw.githubusercontent.com/KOTOR-Community-Portal/mod-builds/dev/content/k2/full.md")
};

using var client = new HttpClient();
client.DefaultRequestHeaders.UserAgent.ParseAdd("kotor-mod-list-puller/1.0");

var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "data");
Directory.CreateDirectory(outputDirectory);

var jsonOptions = new JsonSerializerOptions
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};

var outputs = new List<(string Game, string Source, IReadOnlyList<ModEntry> Mods)>();
foreach (var source in sources)
{
    var markdown = await client.GetStringAsync(source.Url);
    var mods = ModListParser.Parse(markdown);
    var outputPath = Path.Combine(outputDirectory, $"{source.Game}.json");
    var existingMetadata = LoadExistingMetadata(outputPath, jsonOptions);
    var supportedCount = mods.Count(mod => DeadlyStreamClient.CanHandle(mod.Url));
    Console.WriteLine($"Scraping {supportedCount} DeadlyStream entries for {source.Game}...");

    var result = await MetadataEnricher.EnrichAsync(mods, existingMetadata);
    foreach (var error in result.Errors)
    {
        Console.Error.WriteLine($"Warning: {error}");
    }

    outputs.Add((source.Game, source.Url, result.Mods));
}

foreach (var output in outputs)
{
    var document = new ModListOutput(output.Source, output.Mods);
    var outputPath = Path.Combine(outputDirectory, $"{output.Game}.json");
    var json = JsonSerializer.Serialize(document, jsonOptions) + Environment.NewLine;

    await File.WriteAllTextAsync(outputPath, json, new UTF8Encoding(false));
    Console.WriteLine($"Wrote {output.Mods.Count} entries to {outputPath}");
}

static IReadOnlyDictionary<string, ModMetadata> LoadExistingMetadata(
    string path,
    JsonSerializerOptions jsonOptions)
{
    if (!File.Exists(path))
    {
        return new Dictionary<string, ModMetadata>();
    }

    var existing = JsonSerializer.Deserialize<ModListOutput>(File.ReadAllText(path), jsonOptions);
    return existing?.Mods
        .Where(mod => mod.Metadata is not null)
        .ToDictionary(mod => mod.Url, mod => mod.Metadata!, StringComparer.OrdinalIgnoreCase)
        ?? new Dictionary<string, ModMetadata>();
}

internal sealed record ModListOutput(string Source, IReadOnlyList<ModEntry> Mods);
