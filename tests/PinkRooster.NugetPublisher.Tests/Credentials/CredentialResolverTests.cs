using System.Text;
using AwesomeAssertions;
using NuGet.Configuration;
using PinkRooster.NugetPublisher.Credentials;

namespace PinkRooster.NugetPublisher.Tests.Credentials;

public sealed class CredentialResolverTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "pn_cred_test_" + Guid.NewGuid().ToString("N")
    );

    public CredentialResolverTests()
    {
        Directory.CreateDirectory(_tempDir);
        CredentialResolver.ClearActiveKeys();
    }

    public void Dispose()
    {
        CredentialResolver.ClearActiveKeys();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_WhenExplicitKeyProvided_ReturnsExplicitKey()
    {
        // Arrange
        const string feedUrl = "https://api.nuget.org/v3/index.json";
        const string explicitKey = "oy2_explicit_test_key_12345678901234567890";

        // Act
        ResolvedCredential credential = CredentialResolver.Resolve(feedUrl, explicitKey);

        // Assert
        credential.ApiKey.Should().Be(explicitKey);
        credential.Source.Should().Be(CredentialSources.Explicit);
        credential.FeedUrl.Should().Be(feedUrl);
        credential.HeaderName.Should().Be("X-NuGet-ApiKey");
        credential.HeaderValue.Should().Be(explicitKey);
        credential.HasCredentials.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WhenAmbientEnvSet_ResolvesFromEnvironment()
    {
        // 1. nuget.org / generic feed with NUGET_API_KEY
        var envMap1 = new Dictionary<string, string>
        {
            ["NUGET_API_KEY"] = "oy2_ambient_nuget_key_1234567890123456",
        };
        ResolvedCredential cred1 = CredentialResolver.Resolve(
            "https://api.nuget.org/v3/index.json",
            explicitKey: null,
            getEnvironmentVariable: key => envMap1.GetValueOrDefault(key)
        );

        cred1.ApiKey.Should().Be("oy2_ambient_nuget_key_1234567890123456");
        cred1.Source.Should().Be(CredentialSources.Environment);
        cred1.HeaderName.Should().Be("X-NuGet-ApiKey");
        cred1.HeaderValue.Should().Be("oy2_ambient_nuget_key_1234567890123456");
        cred1.HasCredentials.Should().BeTrue();

        // 2. GitHub Packages feed with GH_TOKEN
        var envMap2 = new Dictionary<string, string>
        {
            ["GH_TOKEN"] = "ghp_mockGitHubToken1234567890123456789012",
        };
        ResolvedCredential cred2 = CredentialResolver.Resolve(
            "https://nuget.pkg.github.com/myorg/index.json",
            explicitKey: null,
            getEnvironmentVariable: key => envMap2.GetValueOrDefault(key)
        );

        cred2.ApiKey.Should().Be("ghp_mockGitHubToken1234567890123456789012");
        cred2.Source.Should().Be(CredentialSources.Environment);
        cred2.HeaderName.Should().Be("X-NuGet-ApiKey");
        cred2.HeaderValue.Should().Be("ghp_mockGitHubToken1234567890123456789012");
        cred2.HasCredentials.Should().BeTrue();

        // 3. Azure Artifacts feed with AZURE_DEVOPS_EXT_PAT
        var envMap3 = new Dictionary<string, string>
        {
            ["AZURE_DEVOPS_EXT_PAT"] = "mockAzureDevOpsPat12345678901234567890123456789012",
        };
        ResolvedCredential cred3 = CredentialResolver.Resolve(
            "https://pkgs.dev.azure.com/myorg/_packaging/myfeed/nuget/v3/index.json",
            explicitKey: null,
            getEnvironmentVariable: key => envMap3.GetValueOrDefault(key)
        );

        cred3.ApiKey.Should().Be("mockAzureDevOpsPat12345678901234567890123456789012");
        cred3.Source.Should().Be(CredentialSources.Environment);
        cred3.HeaderName.Should().Be("Authorization");
        string expectedBasicAuth =
            "Basic "
            + Convert.ToBase64String(
                Encoding.ASCII.GetBytes(":mockAzureDevOpsPat12345678901234567890123456789012")
            );
        cred3.HeaderValue.Should().Be(expectedBasicAuth);
        cred3.HasCredentials.Should().BeTrue();

        // 4. Azure Artifacts feed with SYSTEM_ACCESSTOKEN (Bearer token)
        var envMap4 = new Dictionary<string, string>
        {
            ["SYSTEM_ACCESSTOKEN"] = "mockSystemAccessToken123456",
        };
        ResolvedCredential cred4 = CredentialResolver.Resolve(
            "https://myorg.pkgs.visualstudio.com/_packaging/myfeed/nuget/v3/index.json",
            explicitKey: null,
            getEnvironmentVariable: key => envMap4.GetValueOrDefault(key)
        );

        cred4.ApiKey.Should().Be("mockSystemAccessToken123456");
        cred4.Source.Should().Be(CredentialSources.Environment);
        cred4.HeaderName.Should().Be("Authorization");
        cred4.HeaderValue.Should().Be("Bearer mockSystemAccessToken123456");
        cred4.HasCredentials.Should().BeTrue();
    }

    [Fact]
    public void Sanitize_WhenMessageContainsApiKeyOrPat_MasksSecrets()
    {
        // NuGet key pattern (oy2...)
        string nugetMessage =
            "HTTP 403 Forbidden: Invalid key oy2a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v for repository.";
        string sanitizedNuget = CredentialResolver.Sanitize(nugetMessage);
        sanitizedNuget.Should().NotContain("oy2a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v");
        sanitizedNuget.Should().Contain("***REDACTED***");
        sanitizedNuget
            .Should()
            .Be("HTTP 403 Forbidden: Invalid key ***REDACTED*** for repository.");

        // GitHub personal access token (ghp_...)
        string ghpMessage =
            "Failed authentication with token ghp_1234567890abcdefghijklmnopqrstuvwxyz12 to GitHub Packages.";
        string sanitizedGhp = CredentialResolver.Sanitize(ghpMessage);
        sanitizedGhp.Should().NotContain("ghp_1234567890abcdefghijklmnopqrstuvwxyz12");
        sanitizedGhp.Should().Contain("***REDACTED***");

        // GitHub fine-grained PAT (github_pat_...)
        string githubPatMessage =
            "Error using github_pat_11ABCD0123456789_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOP1234567890";
        string sanitizedGithubPat = CredentialResolver.Sanitize(githubPatMessage);
        sanitizedGithubPat
            .Should()
            .NotContain(
                "github_pat_11ABCD0123456789_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOP1234567890"
            );
        sanitizedGithubPat.Should().Contain("***REDACTED***");

        // Azure DevOps 52-char PAT
        string azPat = "4v5k6l7m8n9o0p1q2r3s4t5u6v7w8x9y0z1a2b3c4d5e6f7g8h9i"; // 52 chars
        string azMessage = $"Azure Artifacts push failed with PAT {azPat}";
        string sanitizedAz = CredentialResolver.Sanitize(azMessage);
        sanitizedAz.Should().NotContain(azPat);
        sanitizedAz.Should().Contain("***REDACTED***");

        // Bearer token
        string bearerMessage =
            "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.mockTokenValue";
        string sanitizedBearer = CredentialResolver.Sanitize(bearerMessage);
        sanitizedBearer.Should().NotContain("mockTokenValue");
        sanitizedBearer.Should().Contain("Bearer ***REDACTED***");

        // Basic auth
        string basicMessage = "Authorization: Basic dXNlcm5hbWU6cGFzc3dvcmQxMjM=";
        string sanitizedBasic = CredentialResolver.Sanitize(basicMessage);
        sanitizedBasic.Should().NotContain("dXNlcm5hbWU6cGFzc3dvcmQxMjM=");
        sanitizedBasic.Should().Contain("Basic ***REDACTED***");

        // Active resolved key masking (even if arbitrary string)
        const string customSecret = "super-secret-custom-token-xyz987";
        CredentialResolver.Resolve("https://custom.feed/v3/index.json", customSecret);
        string customErrorMessage = $"Feed rejected custom credential: {customSecret} on endpoint.";
        string sanitizedCustom = CredentialResolver.Sanitize(customErrorMessage);
        sanitizedCustom.Should().NotContain(customSecret);
        sanitizedCustom.Should().Contain("***REDACTED***");
    }

    [Fact]
    public void Resolve_WhenNuGetConfigHasMatchingCredentials_ResolvesFromConfig()
    {
        // Arrange: create a temporary nuget.config
        string configPath = Path.Combine(_tempDir, "nuget.config");
        const string feedUrl = "https://pkgs.example.com/v3/index.json";
        const string nugetConfigContent = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="ExampleFeed" value="https://pkgs.example.com/v3/index.json" />
              </packageSources>
              <packageSourceCredentials>
                <ExampleFeed>
                  <add key="Username" value="feeduser" />
                  <add key="ClearTextPassword" value="feedsecret123" />
                </ExampleFeed>
              </packageSourceCredentials>
            </configuration>
            """;
        File.WriteAllText(configPath, nugetConfigContent);

        ISettings settings = Settings.LoadSpecificSettings(_tempDir, "nuget.config");

        // Act
        ResolvedCredential cred = CredentialResolver.Resolve(
            feedUrl,
            explicitKey: null,
            getEnvironmentVariable: _ => null,
            settings: settings
        );

        // Assert
        cred.ApiKey.Should().Be("feedsecret123");
        cred.Source.Should().Be(CredentialSources.NuGetConfig);
        cred.HeaderName.Should().Be("Authorization");
        string expectedBasic =
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("feeduser:feedsecret123"));
        cred.HeaderValue.Should().Be(expectedBasic);
        cred.HasCredentials.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WhenNoCredentialsFound_ReturnsNone()
    {
        // Act
        ResolvedCredential cred = CredentialResolver.Resolve(
            "https://unauthenticated.example.com/v3/index.json",
            explicitKey: null,
            getEnvironmentVariable: _ => null,
            settings: Settings.LoadDefaultSettings(_tempDir)
        );

        // Assert
        cred.ApiKey.Should().BeNull();
        cred.Source.Should().Be(CredentialSources.None);
        cred.HeaderName.Should().BeNull();
        cred.HeaderValue.Should().BeNull();
        cred.HasCredentials.Should().BeFalse();
    }

    [Fact]
    public void Sanitize_WhenMessageNullOrEmpty_ReturnsOriginalOrEmpty()
    {
        CredentialResolver.Sanitize(null).Should().Be(string.Empty);
        CredentialResolver.Sanitize(string.Empty).Should().Be(string.Empty);
        CredentialResolver
            .Sanitize("Clean error message with no secrets")
            .Should()
            .Be("Clean error message with no secrets");
    }
}
