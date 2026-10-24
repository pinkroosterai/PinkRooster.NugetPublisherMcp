namespace PinkRooster.NugetPublisherMcp.Samples.Hello;

public static class Program
{
    public static async Task Main() => Console.WriteLine(await RunAsync());

    public static Task<string> RunAsync()
    {
        // quickstart:start
        Greeter greeter = new();
        string greeting = greeter.Greet("world");
        // quickstart:end
        return Task.FromResult(greeting);
    }
}
