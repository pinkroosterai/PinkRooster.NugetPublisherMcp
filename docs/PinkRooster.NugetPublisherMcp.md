<img src="https://raw.githubusercontent.com/pinkroosterai/PinkRooster/a3815ce62a2491a8ee1c7401e1e48aa43ef20a85/assets/logo.png" alt="PinkRooster logo" width="120" />

# PinkRooster.NugetPublisherMcp

Production-grade Model Context Protocol (MCP) server for NuGet package inspection, publishing, auditing, and administration.

Status: preview (`0.1.0-preview.1`), developed against the .NET 10 SDK.

```text
dotnet add package PinkRooster.NugetPublisherMcp --prerelease
```

## Quickstart

Run instantly with `dnx`:

```bash
dnx PinkRooster.NugetPublisherMcp
```

### Agent Configuration

Configure in Claude Code, Cursor, or Antigravity:

```json
{
  "mcpServers": {
    "nuget-publisher": {
      "command": "dnx",
      "args": ["PinkRooster.NugetPublisherMcp", "--yes"]
    }
  }
}
```

### Tools

1. `nuget_inspect_local` (ReadOnly): Inspects local `.nupkg` archive without publishing.
2. `nuget_validate_publish` (ReadOnly): Evaluates pre-flight publish safety checklist against target feeds.
3. `nuget_publish` (Destructive): Publishes package and companion symbol package with secret sanitization.
4. `nuget_search` (ReadOnly): Searches NuGet v3 package feeds with filters and pagination.
5. `nuget_get_metadata` (ReadOnly): Retrieves package registration metadata from NuGet v3 feeds.
6. `nuget_audit_vulnerabilities` (ReadOnly): Audits package dependencies against known security advisories.
7. `nuget_manage_version` (Destructive): Administers package version visibility (unlist / relist).
8. `nuget_get_deprecation_url` (ReadOnly): Generates deprecation management URL for nuget.org portal.

### C# Quickstart

```csharp
Greeter greeter = new();
string greeting = greeter.Greet("world");
```

## Compatibility

.NET 10 SDK. Supports Windows and Linux.
