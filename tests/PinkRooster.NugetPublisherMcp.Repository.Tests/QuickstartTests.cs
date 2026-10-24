using AwesomeAssertions;

namespace PinkRooster.NugetPublisherMcp.Repository.Tests;

/// <summary>Keeps the README quickstart identical to the code the Hello sample runs, so the README never shows code that does not compile.</summary>
public sealed class QuickstartTests
{
    [Fact]
    public void Readme_Quickstart_MatchesSample()
    {
        string root = FindRepositoryRoot();
        string readme = Read(root, "README.md");
        string sample = Read(root, "samples/PinkRooster.NugetPublisherMcp.Samples.Hello/Program.cs");

        string shown = Lines(Between(readme, "<!-- quickstart:start -->", "<!-- quickstart:end -->")
            .Split("```", StringSplitOptions.None)[1].Split('\n', 2)[1]);
        string run = Lines(Between(sample, "// quickstart:start", "// quickstart:end"));

        shown.Should().Be(run);
    }

    private static string Between(string text, string start, string end)
    {
        int from = text.IndexOf(start, StringComparison.Ordinal);
        int to = text.IndexOf(end, StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from, $"'{start}' and '{end}' not found in that order");
        return text[(from + start.Length)..to];
    }

    private static string Lines(string text) =>
        string.Join('\n', text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));

    private static string Read(string root, string relative) =>
        File.ReadAllText(Path.Combine(root, relative)).ReplaceLineEndings("\n");

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PinkRooster.NugetPublisherMcp.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("PinkRooster.NugetPublisherMcp.slnx not found above the test output folder.");
    }
}
