using AwesomeAssertions;
using PinkRooster.NugetPublisherMcp.Samples.Hello;

namespace PinkRooster.NugetPublisherMcp.Samples.Tests;

public sealed class HelloSampleTests
{
    [Fact]
    public async Task RunAsync_ReturnsDefaultServiceIndexUrl() =>
        (await Program.RunAsync()).Should().Be("https://api.nuget.org/v3/index.json");
}
