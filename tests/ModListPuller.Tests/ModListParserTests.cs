using ModListPuller;
using Xunit;
using DeadlyScraper;

namespace ModListPuller.Tests;

public class ModListParserTests
{
    private const string Sample = """
        # KOTOR 1 Full Build

        [setup guide](https://example.com/not-a-mod)

        ## Mod List

        ### Character Startup Changes

        **Name:** [Character Startup Changes](https://example.com/mod) and [**Patch**](https://example.com/patch)

        **Author:** An Author

        ### Multi-part Mod

        **Name:** [Multi-part Mod](https://example.com/part-1) and [Part 2](https://example.com/part-2)
        """;

    [Fact]
    public void IgnoresLinksOutsideNameFields()
    {
        var urls = ModListParser.Parse(Sample).Select(mod => mod.Url);

        Assert.DoesNotContain("https://example.com/not-a-mod", urls);
    }

    [Fact]
    public void CreatesAnEntryForEachNameFieldLink()
    {
        var expected = new[]
        {
            new ModEntry("Character Startup Changes", "https://example.com/mod"),
            new ModEntry("Character Startup Changes [Patch]", "https://example.com/patch"),
            new ModEntry("Multi-part Mod", "https://example.com/part-1"),
            new ModEntry("Multi-part Mod - Part 2", "https://example.com/part-2")
        };

        Assert.Equal(expected, ModListParser.Parse(Sample));
    }

    [Fact]
    public void RequiresModListHeading()
    {
        Assert.Throws<ArgumentException>(() =>
            ModListParser.Parse("**Name:** [A Mod](https://example.com/mod)"));
    }

    [Theory]
    [InlineData("https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/", true)]
    [InlineData("https://www.nexusmods.com/kotor/mods/1367", false)]
    [InlineData("https://mega.nz/file/example", false)]
    public void IdentifiesSupportedMetadataUrls(string url, bool expected)
    {
        Assert.Equal(expected, DeadlyStreamClient.CanHandle(url));
    }

    [Fact]
    public void MapsDeadlyStreamMetadata()
    {
        var source = new DeadlyStreamFileMetadata
        {
            SourceUrl = "https://deadlystream.com/files/file/1-example/",
            Title = "Example Mod",
            Author = "Example Author",
            LatestVersion = "1.2",
            LatestVersionReleaseDate = "2026-01-02T00:00:00Z",
            OriginalUploadDate = "2025-01-02T00:00:00Z",
            VersionHistory =
            [
                new DeadlyStreamVersionInfo
                {
                    VersionLabel = "1.2",
                    ChangelogUrl = "https://deadlystream.com/files/file/1-example/?changelog=0"
                }
            ],
            AvailableDownloads =
            [
                new DeadlyStreamDownloadOption
                {
                    FileName = "example.zip",
                    RemoteFileId = "42"
                }
            ]
        };

        var metadata = ModMetadata.From(source);

        Assert.Equal("Example Mod", metadata.Title);
        Assert.Equal("1.2", metadata.VersionHistory);
        Assert.Equal(new ModDownload("example.zip", "42"), Assert.Single(metadata.AvailableDownloads));
    }
}
