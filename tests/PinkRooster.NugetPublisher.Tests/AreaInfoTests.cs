using AwesomeAssertions;

namespace PinkRooster.NugetPublisher.Tests;

public sealed class AreaInfoTests
{
    [Fact]
    public void Name_DefaultsToThePackageId() =>
        new AreaInfo().Name.Should().Be("PinkRooster.NugetPublisher");
}
