namespace PinkRooster.NugetPublisherMcp;

/// <summary>Greets people by name.</summary>
public sealed class Greeter
{
    /// <summary>The word the greeting starts with.</summary>
    public string Salutation { get; init; } = "Hello";

    /// <summary>Builds a greeting for <paramref name="name"/>.</summary>
    /// <param name="name">Who to greet.</param>
    /// <returns>The greeting.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or white space.</exception>
    public string Greet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return $"{Salutation}, {name}!";
    }
}
