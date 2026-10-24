![PinkRooster logo](https://raw.githubusercontent.com/pinkroosterai/PinkRooster/5b6d303a7ab57cf6374b210eaf0e36ca9c2ef403/assets/logo-120.png)

# PinkRooster.NugetPublisherMcp

Production-grade Model Context Protocol (MCP) server for NuGet package inspection, publishing, auditing, and administration.

Status: preview (`0.1.0-preview.2`), developed against the .NET 10 SDK.

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
5. `nuget_list_published` (ReadOnly): Lists published packages for a specific owner, author, or organization with version history and download metrics.
6. `nuget_get_metadata` (ReadOnly): Retrieves package registration metadata from NuGet v3 feeds.
7. `nuget_audit_vulnerabilities` (ReadOnly): Audits package dependencies against known security advisories.
8. `nuget_manage_version` (Destructive): Administers package version visibility (unlist / relist).
9. `nuget_get_deprecation_url` (ReadOnly): Generates deprecation management URL for nuget.org portal.

## Compatibility

.NET 10 SDK. Supports Windows and Linux.
