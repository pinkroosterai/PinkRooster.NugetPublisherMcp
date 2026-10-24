<div align="center">

<img src="assets/logo.png" alt="PinkRooster logo" width="160" />

# PinkRooster.NugetPublisherMcp

**Production-grade Model Context Protocol (MCP) server for NuGet package inspection, publishing, auditing, and administration.**

[![CI](https://github.com/pinkroosterai/PinkRooster.NugetPublisherMcp/actions/workflows/ci.yml/badge.svg)](https://github.com/pinkroosterai/PinkRooster.NugetPublisherMcp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/PinkRooster.NugetPublisherMcp.svg)](https://www.nuget.org/packages/PinkRooster.NugetPublisherMcp)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

[The idea](#the-idea) · [Install](#install) · [Quickstart](#quickstart) · [Packages](#packages) · [Samples](#samples) · [Docs](#docs) · [Contributing](#feedback-contributing-security-and-license)

</div>

---

## Status

Preview (`0.1.0-preview.1`), developed against the .NET 10 SDK on Linux and Windows.

## The idea

Publishing and administering NuGet packages from AI coding agents (Claude Code, Cursor, Antigravity, GitHub Copilot) presents critical safety and operational challenges: leaking API keys into agent conversation logs, pushing unvalidated packages with version collisions or missing symbols, making accidental destructive unlisting calls, or failing to audit dependencies for security advisories prior to release.

`PinkRooster.NugetPublisherMcp` bridges this gap by exposing a secure, production-grade Model Context Protocol (MCP) server over standard I/O. It gives agents structured, granular tools to inspect local `.nupkg` archives, validate publish prerequisites against live feeds, perform safe push operations with companion symbol sequence handling, audit package vulnerabilities, and manage package listing states—all with automatic credential masking and defense-in-depth secret sanitization.

## Install

Run instantly without pre-installation using `dnx` (.NET 10 SDK):

```bash
dnx PinkRooster.NugetPublisherMcp
```

Or install globally as a .NET tool:

```bash
dotnet tool install --global PinkRooster.NugetPublisherMcp --prerelease
```

Or reference the core library in your .NET project:

```text
dotnet add package PinkRooster.NugetPublisher --prerelease
```

## Quickstart

### Running with dnx

Launch the MCP server directly over stdio:

```bash
dnx PinkRooster.NugetPublisherMcp
```

### Agent & IDE Configuration

#### Claude Code (`~/.claude.json` or `.claude.json`)

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

#### Cursor (`.cursor/mcp.json`)

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

#### Antigravity Configuration

Add the server to your agent MCP configuration:

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

### Granular MCP Tools

The server provides 8 granular tools covering the entire package lifecycle:

1. **`nuget_inspect_local`** (`ReadOnly`): Inspects local `.nupkg` archive without publishing. Extracts Nuspec metadata, dependency groups, package files, symbol files, and embedded/remote license and repository information.
2. **`nuget_validate_publish`** (`ReadOnly`): Runs pre-flight safety checks before publishing. Verifies feed reachability, valid authentication credentials, package ID reservation or prefix hijacking, version collision (409), SemVer rules, and symbols.
3. **`nuget_publish`** (`Destructive`): Safely pushes package (`.nupkg`) and optional companion symbol package (`.snupkg`) to target feed (nuget.org, GitHub Packages, Azure Artifacts, BaGet) with automatic duplicate handling (`skipDuplicate`), credential resolution, and defense-in-depth secret sanitization.
4. **`nuget_search`** (`ReadOnly`): Searches NuGet v3 package feeds using search query, filtering by prerelease status, package type, frameworks, and pagination (`skip`/`take`).
5. **`nuget_get_metadata`** (`ReadOnly`): Retrieves comprehensive package registration metadata from NuGet v3 feed: latest versions, release dates, authors, tags, project/license URLs, deprecation notices, vulnerabilities, and listed flags.
6. **`nuget_audit_vulnerabilities`** (`ReadOnly`): Queries NuGet v3 vulnerability database (GitHub Advisory Database / OSV backed) to audit packages or exact versions for known security vulnerabilities with severity ratings and advisory links.
7. **`nuget_manage_version`** (`Destructive`): Administers package version visibility (unlisting or relisting) on NuGet feeds with dry-run safety simulation and explicit 405 MethodNotAllowed diagnostics for feeds that do not support HTTP DELETE.
8. **`nuget_get_deprecation_url`** (`ReadOnly`): Generates direct web portal links to nuget.org with pre-filled guidance parameters for deprecating or transferring package versions safely.

### Programmatic C# API

<!-- quickstart:start -->
```csharp
Greeter greeter = new();
string greeting = greeter.Greet("world");
```
<!-- quickstart:end -->

## Packages

| Package | What it adds | Dependencies |
|---|---|---|
| [`PinkRooster.NugetPublisherMcp`](https://www.nuget.org/packages/PinkRooster.NugetPublisherMcp) | .NET global tool and MCP stdio server providing 8 tools for package inspection, publishing, auditing, and administration. | `ModelContextProtocol`, `Microsoft.Extensions.Hosting`, `PinkRooster.NugetPublisher` |
| [`PinkRooster.NugetPublisher`](https://www.nuget.org/packages/PinkRooster.NugetPublisher) | Core reusable library for NuGet feed protocol interactions, package validation, archive inspection, auditing, and credential resolution. | `NuGet.Protocol`, `NuGet.Packaging`, `NuGet.Configuration` |

## Samples

See [samples/README.md](samples/README.md).

## Docs

- [PinkRooster.NugetPublisherMcp](docs/PinkRooster.NugetPublisherMcp.md): .NET tool and stdio MCP server for agentic IDEs and CLI assistants.
- [PinkRooster.NugetPublisher](docs/PinkRooster.NugetPublisher.md): Core library API documentation and usage patterns.

## Compatibility

.NET 10 SDK; tested on Ubuntu and Windows.

## Build and test

```text
dotnet build PinkRooster.NugetPublisherMcp.slnx -c Release
dotnet test --solution PinkRooster.NugetPublisherMcp.slnx -c Release
```

## Feedback, contributing, security and license

Read [CONTRIBUTING.md](CONTRIBUTING.md) first. Report a vulnerability as [SECURITY.md](SECURITY.md) describes. The code is [MIT](LICENSE) licensed.
