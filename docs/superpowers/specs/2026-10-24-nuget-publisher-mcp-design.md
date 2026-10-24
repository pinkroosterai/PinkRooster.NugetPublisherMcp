# Design Document: PinkRooster.NugetPublisherMcp

**Document Path**: `docs/superpowers/specs/2026-10-24-nuget-publisher-mcp-design.md`  
**Status**: Ready for Approval  
**Date**: 2026-10-24  
**Author**: PinkRooster AI Team & Partner  

---

## 1. Intent and Why

Autonomous AI coding agents (such as Antigravity, Claude Code, Cursor, and GitHub Copilot) frequently handle package authoring, version bumping, release workflows, and dependency maintenance. Today, agents interacting with NuGet feeds are forced to invoke ad-hoc CLI commands (`dotnet nuget`, `curl`) via shell processes. This approach is brittle, vulnerable to silent failures, lacks structured JSON output for deep reasoning, and poses severe security and reliability risks—specifically:
- Accidental pushes of broken or duplicate package versions to `nuget.org`, which are completely immutable.
- Accidental leakage of sensitive API keys or personal access tokens in LLM conversation histories.
- Lack of programmatic access to package unlisting, vulnerability advisories, and pre-flight validation.

**PinkRooster.NugetPublisherMcp** solves this by providing a production-grade, open-source Model Context Protocol (MCP) server distributed as a .NET Tool on `nuget.org` and runnable on demand via `dnx`. It equips agents with structured, high-signal, guarded tools to inspect, audit, publish, and administer NuGet packages across `nuget.org` and private v3 feeds.

---

## 2. Goals, Non-Goals, and Anti-Goals

### Goals
- **Autonomous Agent Ergonomics**: Expose granular, strongly typed MCP tools over `stdio` transport using the official `ModelContextProtocol` 2.x SDK.
- **Guarded Safety Gates (Stateless Enforcement)**: Require and enforce pre-flight dry-run validation (feed collision check, nuspec schema validation, SPDX license check, companion `.snupkg` verification) before allowing mutating operations (publish, unlist).
- **Secure Credential Resolution**: Resolve API keys and PATs transparently from ambient environment variables (`NUGET_API_KEY`, `GH_TOKEN`, `AZURE_DEVOPS_EXT_PAT`) and hierarchical `nuget.config` files, with automatic secret masking in tool outputs.
- **Multi-Feed Support**: Support `nuget.org` alongside private authenticated NuGet v3 feeds (Azure Artifacts, GitHub Packages, BaGet, ProGet, Artifactory).
- **Automated Symbol Handling**: Automatically detect, pair, and sequence companion `.snupkg` symbol packages adjacent to `.nupkg` archives.
- **On-Demand Execution**: Package as a .NET tool on `nuget.org` runnable seamlessly via `dnx PinkRooster.NugetPublisherMcp` or global tool installation.
- **PinkRooster Craftsmanship**: Adhere strictly to PinkRooster repo standards (.NET 10, C# 14 modern idioms, `PinkRooster.Check` passing at 0 findings, comprehensive xUnit test coverage).

### Non-Goals
- **Deep Assembly Decompilation / IL Disassembly**: The server will not decompile compiled IL binaries or use Roslyn to generate public API surface diffs. Inspection is intentionally focused on feed metadata, nuspec manifests, and package structure.
- **GUI or Web Dashboard**: The server communicates strictly via standard MCP protocols over `stdio`.
- **Feed Hosting**: The tool is a client/publisher and manager for existing feeds, not a NuGet feed server itself.

### Anti-Goals
- **Credential Leakage**: Never echo or leak raw API keys, PATs, or authorization headers into agent conversation transcripts or error responses.
- **Unverified Blind Pushing**: Never push a package without executing pre-flight verification or validating target feed version immutability.
- **Tool Sprawl**: Avoid monolithic "god tools" with complex nested sub-commands that confuse LLM tool calling.

---

## 3. Constraints & Protocol Realities

1. **Platform & Runtime**: .NET 10 (`net10.0`), C# 14 modern language idioms (`field` keyword, null-conditional assignment, modern pattern matching).
2. **PinkRooster Repository Standard**: Solution structure managed via `PinkRooster.Templates`, `Directory.Build.props`, `Directory.Packages.props`, passing all `PinkRooster.Check` rules without unapproved deviations.
3. **NuGet Immutability**: `nuget.org` versions cannot be modified or replaced once uploaded. Safety checks must prevent version collision failures (`409 Conflict`).
4. **Publish & Symbol Sequencing**: NuGet v3 protocol requires pushing the primary `.nupkg` to `PackagePublish` *first*, and only then pushing the companion `.snupkg` to `SymbolPackagePublish`. Reversing the order or pushing symbols for a non-existent package results in `404 Not Found`.
5. **NuGet.org Deprecation API Limitation**: `nuget.org` has no public REST API for programmatic package deprecation (tracked under NuGetGallery #8873; deprecation is strictly enforced in the web UI). The server provides programmatic unlist/relist and a dedicated deprecation portal URL generator with pre-filled guidance.
6. **Private Feed Idiosyncrasies**:
   - Azure Artifacts: Supports `PUT` package push and `DELETE` unlisting; does not support `.snupkg` symbols directly.
   - GitHub Packages: Supports `PUT` package push; does not support `DELETE` unlisting via NuGet V2 endpoint (returns `405 Method Not Allowed`). The tool reports actionable feed-specific diagnostics.
7. **Transport**: Fast startup over MCP standard I/O (`stdio`) with zero extraneous console logging that would corrupt the JSON-RPC stream.

---

## 4. Architectural Approaches

### Approaches Considered

#### Approach 1: Layered Core Library + CLI Tool with Official SDKs (Selected)
- **Architecture**:
  - `src/PinkRooster.NugetPublisher`: Reusable class library containing domain models, NuGet v3 client protocols (`NuGet.Protocol`, `NuGet.Packaging`), pre-flight safety validator, nuspec parser, and credential resolver.
  - `src/PinkRooster.NugetPublisherMcp`: Executable console app configured with `<PackAsTool>true</PackAsTool>` and executable via `dnx`. Hosts the stdio MCP server via `ModelContextProtocol` SDK.
- **Trade-offs**:
  - Full NuGet v3 protocol fidelity, proven credential handling, companion `.snupkg` support, isolated unit testability without MCP overhead.
  - Slightly larger tool package size due to `NuGet.Protocol` dependencies, but standard in the .NET ecosystem.

#### Approach 2: Monolithic Single-Project Tool with Custom Direct HTTP Client
- **Architecture**: Single project using `HttpClient` and `System.Text.Json` to implement custom REST clients for NuGet endpoints and raw `ZipArchive` extraction.
- **Trade-offs**: Smallest binary size, but reinvents complex feed authentication and push protocols, higher maintenance burden, no reusable library.

#### Approach 3: Process Execution Wrapper over `dotnet nuget` CLI
- **Architecture**: MCP facade invoking shell processes.
- **Trade-offs**: Fragile stdout scraping, missing package deprecation and vulnerability APIs, poor reliability. Dropped early.

### Decision
**Approach 1** was selected for its production-grade robustness, protocol fidelity, and clean domain isolation.

---

## 5. In-Depth Specification: Core Mutating Tools

The core value of this MCP server centers on **`nuget_publish`** and **`nuget_manage_version`**. Below is their detailed protocol contract, pre-flight pipeline, error handling, and private feed behaviors.

### 5.1 `nuget_publish`

#### Purpose
Safely publish a `.nupkg` archive (and optional adjacent companion `.snupkg`) to `nuget.org` or a private NuGet v3 feed.

#### Parameters
| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `nupkgPath` | string | Yes | - | Absolute or relative path to the `.nupkg` package archive. |
| `feedUrl` | string | No | `https://api.nuget.org/v3/index.json` | Target NuGet v3 service index URL or v2 push endpoint. |
| `apiKey` | string | No | null | Explicit API key or PAT. If omitted, resolved from ambient env (`NUGET_API_KEY`, `GH_TOKEN`) or `nuget.config`. |
| `symbolFeedUrl` | string | No | null | Custom symbol publish URL. If omitted, discovered from the service index `SymbolPackagePublish/4.9.0` resource. |
| `skipDuplicate` | bool | No | `false` | If `true`, a `409 Conflict` (version already exists) is treated as a non-fatal warning instead of an error. |
| `force` | bool | No | `false` | Bypass pre-flight safety warnings (cannot bypass hard feed rejection errors). |

#### Execution Pipeline (Stateless & Guarded)
1. **Local Archive & Nuspec Audit**:
   - Opens the `.nupkg` via `System.IO.Compression.ZipArchive` / `NuGet.Packaging.PackageArchiveReader`.
   - Reads and validates `.nuspec`: extracts `id`, `version`, `authors`, `description`, `license`, `projectUrl`, `tags`, and dependency groups.
   - Verifies valid SemVer 2.0.0.
   - Validates license expression against SPDX specification (or checks for bundled license file).
2. **Companion Symbol Package Detection**:
   - Looks for an adjacent `.snupkg` file matching the naming convention `{id}.{version}.snupkg`.
   - If present, verifies matching manifest ID and version.
3. **Remote Feed Collision & Pre-Flight Check**:
   - Discovers service endpoints from `{feedUrl}` (`PackagePublish/2.0.0`, `RegistrationsBaseUrl`, `SymbolPackagePublish/4.9.0`).
   - Queries the feed's registration index (`RegistrationsBaseUrl` or `PackageBaseAddress`) to check if `{id}/{version}` already exists.
   - If version exists and `skipDuplicate == false`: Aborts immediately before any HTTP PUT, returning a structured collision blocker.
4. **Primary Package Upload**:
   - Sends HTTP `PUT` to `PackagePublish` endpoint with `Content-Type: multipart/form-data`, stream payload, and `X-NuGet-ApiKey` header (or Authorization header for private feeds).
   - Evaluates HTTP response:
     - `201 Created` / `202 Accepted`: Success.
     - `400 Bad Request`: Returns validation rejection message from feed.
     - `401 Unauthorized` / `403 Forbidden`: Returns permission error, masking any secret keys.
     - `409 Conflict`: Handled per `skipDuplicate` flag.
     - `413 Payload Too Large`: Returns package size error (nuget.org max 250 MB).
5. **Companion Symbol Upload (Sequenced)**:
   - If companion `.snupkg` is detected and target feed provides `SymbolPackagePublish` (or custom `symbolFeedUrl`):
   - Sends HTTP `PUT` to symbol endpoint with companion archive bytes and API key.
   - If symbol upload fails, the main package remains published, but structured warnings are returned in the response.

#### Output Contract
Returns a structured JSON response:
```json
{
  "success": true,
  "packageId": "PinkRooster.NugetPublisher",
  "version": "1.0.0",
  "feedUrl": "https://api.nuget.org/v3/index.json",
  "publishedPackage": true,
  "publishedSymbols": true,
  "symbolPackagePath": "/path/to/PinkRooster.NugetPublisher.1.0.0.snupkg",
  "statusCode": 201,
  "warnings": [],
  "message": "Successfully published PinkRooster.NugetPublisher 1.0.0 and companion symbols to nuget.org."
}
```

---

### 5.2 `nuget_manage_version`

#### Purpose
Administer the visibility and listing lifecycle of published package versions across `nuget.org` and compatible feeds.

#### Parameters
| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `packageId` | string | Yes | - | The package identifier. |
| `version` | string | Yes | - | The specific package version to administer. |
| `action` | string | Yes | - | Lifecycle action: `"unlist"` or `"relist"`. |
| `dryRun` | bool | No | `false` | When `true`, inspects current feed state and previews the mutation without modifying the feed. |
| `feedUrl` | string | No | `https://api.nuget.org/v3/index.json` | Target NuGet v3 feed. |
| `apiKey` | string | No | null | Explicit API key or PAT (defaults to ambient env/nuget.config). |

#### Protocol Mechanics
1. **Unlist Operation (`action == "unlist"`)**:
   - Resolves `PackagePublish/2.0.0` endpoint from service index.
   - In `dryRun` mode: queries `RegistrationsBaseUrl` to check if package exists and its current `listed` boolean flag.
   - In live mode: issues HTTP `DELETE {packagePublishUrl}/{packageId}/{version}` with `X-NuGet-ApiKey` header.
   - Expected status codes:
     - `204 No Content`: Version successfully unlisted (hidden from search/browsing while preserving restore compatibility).
     - `404 Not Found`: Package version does not exist on target feed.
     - `403 Forbidden`: API key lacks unlist scope or insufficient package ownership.
     - `405 Method Not Allowed`: Feed does not support unlisting via NuGet V2 endpoint (e.g. GitHub Packages; returns actionable guidance).
2. **Relist Operation (`action == "relist"`)**:
   - Resolves `PackagePublish/2.0.0` endpoint.
   - In live mode: issues HTTP `POST {packagePublishUrl}/{packageId}/{version}` with `X-NuGet-ApiKey` header.
   - Expected status codes:
     - `200 OK`: Version successfully relisted.
     - `404 Not Found`: Package version does not exist on target feed.
     - `403 Forbidden`: API key lacks relist scope.

#### Output Contract
```json
{
  "success": true,
  "packageId": "PinkRooster.NugetPublisher",
  "version": "1.0.0",
  "action": "unlist",
  "dryRun": false,
  "previousStatus": "Listed",
  "currentStatus": "Unlisted",
  "message": "Package PinkRooster.NugetPublisher 1.0.0 was successfully unlisted from nuget.org."
}
```

---

## 6. Complete MCP Tool Surface

In addition to `nuget_publish` and `nuget_manage_version`, the server provides high-signal supporting tools:

1. **`nuget_search`**: Search packages on feeds by query, ID prefix, or tags (`query`, `take`, `prerelease`, `feedUrl`).
2. **`nuget_get_metadata`**: Retrieve full registration details: versions, authors, tags, license, repository URLs, and dependencies per target framework.
3. **`nuget_audit_vulnerabilities`**: Query known CVEs and security advisories for a package/version across NuGet v3 and GitHub advisory databases.
4. **`nuget_inspect_local`**: Read local `.nupkg` nuspec manifest, validate SemVer, check SPDX license expressions, and verify `.snupkg` presence without touching a network.
5. **`nuget_validate_publish`**: Standalone dry-run preview tool executing pre-flight checks against a target feed without publishing.
6. **`nuget_get_deprecation_url`**: Generate the direct `nuget.org` web portal URL with pre-filled guidance parameters for deprecation, since `nuget.org` enforces deprecation via its web portal.

---

## 7. Reviewer Calls Made

1. **Stateless Pre-Flight Enforcement**: `nuget_publish` executes all validation checks internally as a mandatory step before pushing, keeping the server completely stateless. `nuget_validate_publish` is an explicit on-demand dry-run preview tool for agents.
2. **Deprecation Handling**: Because `nuget.org` lacks a programmatic deprecation API, `nuget_manage_version` handles `unlist` and `relist`, while `nuget_get_deprecation_url` provides direct portal navigation and structured advice for agents and human operators.
3. **Multi-Feed Audit Consistency**: Added optional `feedUrl` to `nuget_audit_vulnerabilities`, defaulting to `https://api.nuget.org/v3/index.json`.
4. **Dry-Run for Version Management**: Added `dryRun: bool = false` to `nuget_manage_version` to allow agents to preview unlist/relist operations before mutating feeds.
5. **Symbol Sequencing & Fallback**: Documented the strict dependency order (primary package must be pushed before symbol package) and feed capability checks.

---

## 8. What's Left to the Builder

- Exact HTTP retry policies and timeout configurations using `Microsoft.Extensions.Http.Resilience` or standard `HttpClient` handlers.
- Implementation details of zip stream reading and XML parsing for older or unusual nuspec schema namespaces.
- Unit test mock strategies (mocking `HttpMessageHandler` and NuGet `SourceRepository` responses).
- PinkRooster CI/CD workflow configuration and release pipeline scripts.
