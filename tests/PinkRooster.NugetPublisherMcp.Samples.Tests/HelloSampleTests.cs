using AwesomeAssertions;
using PinkRooster.NugetPublisherMcp.Samples.Hello;

namespace PinkRooster.NugetPublisherMcp.Samples.Tests;

public sealed class HelloSampleTests
{
    [Fact]
    public async Task RunAsync_Greets() =>
        (await Program.RunAsync()).Should().Be("Hello, world!");
}
