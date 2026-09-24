using System.Net;
using DeadlyScraper;

namespace ModListPuller;

public static class MetadataEnricher
{
    private const int MaxAttempts = 3;

    public static async Task<MetadataEnrichmentResult> EnrichAsync(
        IReadOnlyList<ModEntry> mods,
        IReadOnlyDictionary<string, ModMetadata> existingMetadata,
        CancellationToken cancellationToken = default)
    {
        var enrichedMods = new List<ModEntry>(mods.Count);
        var errors = new List<string>();
        foreach (var mod in mods)
        {
            if (!DeadlyStreamClient.CanHandle(mod.Url))
            {
                enrichedMods.Add(mod);
                continue;
            }

            try
            {
                var metadata = await GetMetadataWithRetryAsync(mod.Url, cancellationToken);
                enrichedMods.Add(mod with { Metadata = ModMetadata.From(metadata) });
            }
            catch (Exception exception)
            {
                errors.Add($"Failed to scrape '{mod.Name}' ({mod.Url}): {exception.Message}");
                enrichedMods.Add(existingMetadata.TryGetValue(mod.Url, out var metadata)
                    ? mod with { Metadata = metadata }
                    : mod);
            }
        }

        return new MetadataEnrichmentResult(enrichedMods, errors);
    }

    private static async Task<DeadlyStreamFileMetadata> GetMetadataWithRetryAsync(
        string url,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var httpClient = new HttpClient();
                var client = new DeadlyStreamClient(httpClient);
                return await client.GetMetadataAsync(url, cancellationToken);
            }
            catch (HttpRequestException exception) when (
                attempt < MaxAttempts &&
                exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 5), cancellationToken);
            }
        }
    }
}

public sealed record MetadataEnrichmentResult(
    IReadOnlyList<ModEntry> Mods,
    IReadOnlyList<string> Errors);

public sealed record ModMetadata(
    string? Title,
    string? Author,
    string? LatestVersion,
    string? LatestVersionReleaseDate,
    string? OriginalUploadDate,
    string VersionHistory,
    IReadOnlyList<ModDownload> AvailableDownloads)
{
    public static ModMetadata From(DeadlyStreamFileMetadata metadata) => new(
        metadata.Title,
        metadata.Author,
        metadata.LatestVersion,
        metadata.LatestVersionReleaseDate,
        metadata.OriginalUploadDate,
        metadata.VersionHistoryText,
        metadata.AvailableDownloads
            .Select(download => new ModDownload(download.FileName, download.RemoteFileId))
            .ToList());
}

public sealed record ModDownload(string FileName, string? RemoteFileId);
