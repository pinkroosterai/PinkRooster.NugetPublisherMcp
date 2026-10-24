using PinkRooster.NugetPublisher.Feeds;

namespace PinkRooster.NugetPublisherMcp.Samples.Hello;

public static class Program
{
    public static async Task Main() => Console.WriteLine(await RunAsync());

    public static async Task<string> RunAsync()
    {
        // quickstart:start
        NugetFeedClient client = new();
        FeedServiceEndpoints endpoints = await client.GetServiceEndpointsAsync();
        // quickstart:end
        return endpoints.ServiceIndexUrl;
    }
}
