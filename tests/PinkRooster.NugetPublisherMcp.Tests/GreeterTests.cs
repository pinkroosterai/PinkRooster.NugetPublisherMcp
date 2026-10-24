using AwesomeAssertions;

namespace PinkRooster.NugetPublisherMcp.Tests;

public sealed class GreeterTests
{
    [Fact]
    public void Greet_ReturnsGreeting() =>
        new Greeter().Greet("world").Should().Be("Hello, world!");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Greet_RejectsBlankName(string name)
    {
        Action greet = () => new Greeter().Greet(name);

        greet.Should().Throw<ArgumentException>();
    }
}
