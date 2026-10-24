# PinkRooster.NugetPublisherMcp Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement and release a production-grade, open-source Model Context Protocol (MCP) server packaged as a .NET tool on `nuget.org` enabling autonomous AI agents to inspect, audit, publish, and administer NuGet packages on `nuget.org` and private feeds via `dnx`.

**Architecture:** Layered architecture consisting of a reusable core library (`PinkRooster.NugetPublisher`) implementing NuGet v3 protocols, pre-flight safety gates, and credential resolution, paired with an executable .NET global/local tool (`PinkRooster.NugetPublisherMcp`) hosting an MCP stdio server via the official `ModelContextProtocol` SDK.

**Tech Stack:** .NET 10 (`net10.0`), C# 14, `ModelContextProtocol` (v2.3.0), `NuGet.Protocol`, `NuGet.Packaging`, xUnit, FluentAssertions, `PinkRooster.Check`.

**Spec:** docs/superpowers/specs/2026-10-24-nuget-publisher-mcp-design.md

## Global Constraints

- Target modern framework: `<TargetFramework>net10.0</TargetFramework>` and `<Nullable>enable</Nullable>`.
- Use modern C# 14 language idioms: contextual `field` keyword for properties with validation, null-conditional assignments (`?.=`, `?+=`), extension blocks, and modern pattern matching.
- Solution structure governed by PinkRooster repository standards; `PinkRooster.Check` must exit 0 findings before completion.
- Stdio transport must remain unpolluted: zero diagnostic logging to standard output (`Console.Out`), all diagnostic logging to standard error (`Console.Error`).
- Strict secret hygiene: API keys and PATs must never appear in unmasked form in MCP tool outputs or exception messages.
- Stateless guarded publishing: `nuget_publish` must internally run pre-flight validation checks before initiating any HTTP PUT upload.

## Review Focus

1. **Feed version collision without force flag**: When a package version already exists on `nuget.org` or private feed and `skipDuplicate == false`, pre-flight validation must fail prior to any HTTP PUT, preventing immutable collision errors.
2. **Missing companion `.snupkg` when symbol push is enabled**: When publishing `.nupkg` without an adjacent `.snupkg`, publication of the primary package must succeed cleanly with a warning rather than failing.
3. **Invalid or malformed SPDX license expression in local `.nuspec`**: When a local package has an invalid SPDX expression, inspection and pre-flight validation must flag the specific error before publishing.
4. **Feed returns 405 Method Not Allowed on unlist (e.g. GitHub Packages)**: When `nuget_manage_version` attempts unlist against feeds that do not support HTTP `DELETE`, the tool must return an actionable diagnostic explaining feed limitations rather than throwing an unhandled crash.
5. **Masking credentials during feed failures**: When a feed returns 401/403 or network exceptions, all diagnostic messages returned to the agent must sanitize and redact any credentials or API keys.

---

## File Structure

```text
PinkRooster.NugetPublisherMcp/
├── Directory.Build.props
├── Directory.Packages.props
├── PinkRooster.NugetPublisherMcp.slnx
├── src/
│   ├── PinkRooster.NugetPublisher/
│   │   ├── PinkRooster.NugetPublisher.csproj
│   │   ├── Packaging/
│   │   │   ├── PackageArchiveInfo.cs
│   │   │   └── PackageArchiveInspector.cs
│   │   ├── Credentials/
│   │   │   ├── ResolvedCredential.cs
│   │   │   └── CredentialResolver.cs
│   │   ├── Feeds/
│   │   │   ├── INugetFeedClient.cs
│   │   │   ├── NugetFeedClient.cs
│   │   │   ├── FeedServiceEndpoints.cs
│   │   │   └── Models/
│   │   │       ├── PackageMetadataResult.cs
│   │   │       ├── PackageSearchResult.cs
│   │   │       └── PushResult.cs
│   │   ├── Validation/
│   │   │   ├── ValidationResult.cs
│   │   │   ├── ValidationIssue.cs
│   │   │   └── PublishSafetyValidator.cs
│   │   ├── Publishing/
│   │   │   ├── IPackagePublisherService.cs
│   │   │   └── PackagePublisherService.cs
│   │   ├── Administration/
│   │   │   ├── IPackageAdministrationService.cs
│   │   │   └── PackageAdministrationService.cs
│   │   └── Vulnerabilities/
│   │       ├── IVulnerabilityAuditService.cs
│   │       └── VulnerabilityAuditService.cs
│   └── PinkRooster.NugetPublisherMcp/
│       ├── PinkRooster.NugetPublisherMcp.csproj
│       ├── Program.cs
│       └── Tools/
│           ├── NugetDiscoveryTools.cs
│           ├── NugetInspectionTools.cs
│           ├── NugetPublishingTools.cs
│           └── NugetAdministrationTools.cs
└── tests/
    ├── PinkRooster.NugetPublisher.Tests/
    │   ├── PinkRooster.NugetPublisher.Tests.csproj
    │   ├── Packaging/PackageArchiveInspectorTests.cs
    │   ├── Credentials/CredentialResolverTests.cs
    │   ├── Feeds/NugetFeedClientTests.cs
    │   ├── Validation/PublishSafetyValidatorTests.cs
    │   ├── Publishing/PackagePublisherServiceTests.cs
    │   ├── Administration/PackageAdministrationServiceTests.cs
    │   ├── Vulnerabilities/VulnerabilityAuditServiceTests.cs
    │   └── Tools/NugetMcpToolsTests.cs
    └── PinkRooster.NugetPublisher.Repository.Tests/
        └── GovernanceTests.cs
```

---

## Tasks

### Task 1: Repository Scaffolding & PinkRooster Template Alignment

**Files:**
- Create: `PinkRooster.NugetPublisherMcp.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `pinkrooster-template.json`
- Create: `src/PinkRooster.NugetPublisher/PinkRooster.NugetPublisher.csproj`
- Create: `src/PinkRooster.NugetPublisherMcp/PinkRooster.NugetPublisherMcp.csproj`
- Create: `tests/PinkRooster.NugetPublisher.Tests/PinkRooster.NugetPublisher.Tests.csproj`

**Interfaces:**
- Consumes: None (scaffolding baseline).
- Produces: Compilable .NET 10 solution referencing `ModelContextProtocol` (2.3.0), `NuGet.Protocol` (6.13.0+), and `NuGet.Packaging` (6.13.0+).

- [ ] **Step 1: Install templates and scaffold PinkRooster repository**
  Run:
  ```bash
  dotnet new install /home/najgeetsrev/Development/PinkRooster.Templates/templates/pinkrooster-repo
  dotnet new install /home/najgeetsrev/Development/PinkRooster.Templates/templates/pinkrooster-package
  dotnet new install /home/najgeetsrev/Development/PinkRooster.Templates/templates/pinkrooster-overlay
  dotnet new pinkrooster-repo -n PinkRooster.NugetPublisherMcp --kind library --summary "Production-grade Model Context Protocol (MCP) server for NuGet package inspection, publishing, auditing, and administration." --tags "nuget mcp ai agent publish dnx" -o .
  dotnet new pinkrooster-overlay -n PinkRooster.NugetPublisherMcp -o .
  ```

- [ ] **Step 2: Add PinkRooster.NugetPublisher core library and wire packages in Directory.Packages.props**
  Configure `Directory.Packages.props` with package versions:
  - `ModelContextProtocol` -> `2.3.0`
  - `NuGet.Protocol` -> `6.13.2`
  - `NuGet.Packaging` -> `6.13.2`
  - `FluentAssertions` -> `8.0.1`

- [ ] **Step 3: Verify build and test of baseline scaffold**
  Run: `dotnet build PinkRooster.NugetPublisherMcp.slnx -c Release`
  Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**
  ```bash
  git add -A
  git commit -m "chore: scaffold PinkRooster.NugetPublisherMcp solution from template"
  ```

---

### Task 2: Local Package Archive Inspector & NuSpec Extractor

**Files:**
- Create: `src/PinkRooster.NugetPublisher/Packaging/PackageArchiveInfo.cs`
- Create: `src/PinkRooster.NugetPublisher/Packaging/PackageArchiveInspector.cs`
- Create: `tests/PinkRooster.NugetPublisher.Tests/Packaging/PackageArchiveInspectorTests.cs`

**Interfaces:**
- Consumes: `System.IO.Compression.ZipArchive`, `System.Xml.Linq`
- Produces: `PackageArchiveInspector.Inspect(string nupkgPath) -> PackageArchiveInfo`

- [ ] **Step 1: Write the failing tests for package inspection**
  In `tests/PinkRooster.NugetPublisher.Tests/Packaging/PackageArchiveInspectorTests.cs`:
  - `Inspect_WhenValidNupkg_ExtractsMetadataAndDependencies`
  - `Inspect_WhenLicenseExpressionInvalid_ReportsValidationFailure` (Review Focus #3)
  - `Inspect_WhenCompanionSnupkgExists_DetectsSymbolPackage`

- [ ] **Step 2: Run test to verify failure**
  Run: `dotnet test --filter "FullyQualifiedName~PackageArchiveInspectorTests" -c Release`
  Expected: FAIL with compilation error (types not defined).

- [ ] **Step 3: Implement `PackageArchiveInfo` and `PackageArchiveInspector`**
  Implement zip archive reading, manifest parsing, SemVer validation, and companion `.snupkg` check in `src/PinkRooster.NugetPublisher/Packaging/PackageArchiveInspector.cs`.

- [ ] **Step 4: Run test to verify passing**
  Run: `dotnet test --filter "FullyQualifiedName~PackageArchiveInspectorTests" -c Release`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/PinkRooster.NugetPublisher/Packaging/ tests/PinkRooster.NugetPublisher.Tests/Packaging/
  git commit -m "feat: implement local nupkg archive and nuspec inspector"
  ```

---

### Task 3: Credential Resolver & Secret Sanitizer

**Files:**
- Create: `src/PinkRooster.NugetPublisher/Credentials/ResolvedCredential.cs`
- Create: `src/PinkRooster.NugetPublisher/Credentials/CredentialResolver.cs`
- Create: `tests/PinkRooster.NugetPublisher.Tests/Credentials/CredentialResolverTests.cs`

**Interfaces:**
- Consumes: Environment variables, `NuGet.Configuration.Settings`
- Produces:
  - `CredentialResolver.Resolve(string? feedUrl, string? explicitKey) -> ResolvedCredential`
  - `CredentialResolver.Sanitize(string message) -> string`

- [ ] **Step 1: Write failing tests for credential resolution and sanitization**
  In `tests/PinkRooster.NugetPublisher.Tests/Credentials/CredentialResolverTests.cs`:
  - `Resolve_WhenExplicitKeyProvided_ReturnsExplicitKey`
  - `Resolve_WhenAmbientEnvSet_ResolvesFromEnvironment`
  - `Sanitize_WhenMessageContainsApiKeyOrPat_MasksSecrets` (Review Focus #5)

- [ ] **Step 2: Run test to verify failure**
  Run: `dotnet test --filter "FullyQualifiedName~CredentialResolverTests" -c Release`
  Expected: FAIL with compilation error.

- [ ] **Step 3: Implement `CredentialResolver`**
  Implement ambient env lookup (`NUGET_API_KEY`, `GH_TOKEN`, `AZURE_DEVOPS_EXT_PAT`), `nuget.config` fallback, and regex masking in `src/PinkRooster.NugetPublisher/Credentials/CredentialResolver.cs`.

- [ ] **Step 4: Run test to verify passing**
  Run: `dotnet test --filter "FullyQualifiedName~CredentialResolverTests" -c Release`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/PinkRooster.NugetPublisher/Credentials/ tests/PinkRooster.NugetPublisher.Tests/Credentials/
  git commit -m "feat: implement credential resolver with automatic secret sanitization"
  ```

---

### Task 4: NuGet v3 Feed Protocol Client

**Files:**
- Create: `src/PinkRooster.NugetPublisher/Feeds/INugetFeedClient.cs`
- Create: `src/PinkRooster.NugetPublisher/Feeds/NugetFeedClient.cs`
- Create: `src/PinkRooster.NugetPublisher/Feeds/Models/*.cs`
- Create: `tests/PinkRooster.NugetPublisher.Tests/Feeds/NugetFeedClientTests.cs`

**Interfaces:**
- Consumes: `NuGet.Protocol`, `System.Net.Http.HttpClient`
- Produces:
  - `INugetFeedClient.GetPackageMetadataAsync(string packageId, string feedUrl, CancellationToken ct) -> Task<PackageMetadataResult>`
  - `INugetFeedClient.SearchPackagesAsync(string query, int take, bool prerelease, string feedUrl, CancellationToken ct) -> Task<IReadOnlyList<PackageSearchResult>>`
  - `INugetFeedClient.DoesVersionExistAsync(string packageId, string version, string feedUrl, CancellationToken ct) -> Task<bool>`
  - `INugetFeedClient.PushPackageAsync(Stream stream, string feedUrl, string apiKey, CancellationToken ct) -> Task<PushResult>`
  - `INugetFeedClient.PushSymbolPackageAsync(Stream stream, string symbolFeedUrl, string apiKey, CancellationToken ct) -> Task<PushResult>`
  - `INugetFeedClient.UnlistPackageAsync(string packageId, string version, string feedUrl, string apiKey, CancellationToken ct) -> Task<HttpResponseMessage>`
  - `INugetFeedClient.RelistPackageAsync(string packageId, string version, string feedUrl, string apiKey, CancellationToken ct) -> Task<HttpResponseMessage>`

- [ ] **Step 1: Write failing tests for feed operations**
  In `tests/PinkRooster.NugetPublisher.Tests/Feeds/NugetFeedClientTests.cs`:
  - Test registration metadata parsing
  - Test search results parsing
  - Test package push HTTP PUT multipart formatting and header injection
  - Test unlist DELETE and relist POST requests

- [ ] **Step 2: Run test to verify failure**
  Run: `dotnet test --filter "FullyQualifiedName~NugetFeedClientTests" -c Release`
  Expected: FAIL with compilation error.

- [ ] **Step 3: Implement `INugetFeedClient` and `NugetFeedClient`**
  Implement Service Index discovery (`PackagePublish/2.0.0`, `RegistrationsBaseUrl`, `SymbolPackagePublish/4.9.0`), HTTP execution with mocked handlers, and model mapping.

- [ ] **Step 4: Run test to verify passing**
  Run: `dotnet test --filter "FullyQualifiedName~NugetFeedClientTests" -c Release`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/PinkRooster.NugetPublisher/Feeds/ tests/PinkRooster.NugetPublisher.Tests/Feeds/
  git commit -m "feat: implement NuGet v3 feed client protocol"
  ```

---

### Task 5: Pre-Flight Safety & Dry-Run Validator

**Files:**
- Create: `src/PinkRooster.NugetPublisher/Validation/ValidationResult.cs`
- Create: `src/PinkRooster.NugetPublisher/Validation/ValidationIssue.cs`
- Create: `src/PinkRooster.NugetPublisher/Validation/PublishSafetyValidator.cs`
- Create: `tests/PinkRooster.NugetPublisher.Tests/Validation/PublishSafetyValidatorTests.cs`

**Interfaces:**
- Consumes: `PackageArchiveInspector`, `INugetFeedClient`
- Produces: `PublishSafetyValidator.ValidateAsync(string nupkgPath, string feedUrl, CancellationToken ct) -> Task<ValidationResult>`

- [ ] **Step 1: Write failing tests for pre-flight validation**
  In `tests/PinkRooster.NugetPublisher.Tests/Validation/PublishSafetyValidatorTests.cs`:
  - `Validate_WhenPackageValidAndVersionDoesNotExist_Passes`
  - `Validate_WhenPackageVersionAlreadyExistsOnFeed_FailsValidation` (Review Focus #1)
  - `Validate_WhenNuspecMalformed_FailsValidation`

- [ ] **Step 2: Run test to verify failure**
  Run: `dotnet test --filter "FullyQualifiedName~PublishSafetyValidatorTests" -c Release`
  Expected: FAIL

- [ ] **Step 3: Implement `PublishSafetyValidator`**
  Wire local archive inspection with feed collision detection into structured `ValidationResult`.

- [ ] **Step 4: Run test to verify passing**
  Run: `dotnet test --filter "FullyQualifiedName~PublishSafetyValidatorTests" -c Release`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/PinkRooster.NugetPublisher/Validation/ tests/PinkRooster.NugetPublisher.Tests/Validation/
  git commit -m "feat: implement pre-flight publish safety validator"
  ```

---

### Task 6: Mutating Service: Package Publisher & Companion Symbol Sequencer

**Files:**
- Create: `src/PinkRooster.NugetPublisher/Publishing/IPackagePublisherService.cs`
- Create: `src/PinkRooster.NugetPublisher/Publishing/PackagePublisherService.cs`
- Create: `src/PinkRooster.NugetPublisher/Publishing/PublishPackageResponse.cs`
- Create: `tests/PinkRooster.NugetPublisher.Tests/Publishing/PackagePublisherServiceTests.cs`

**Interfaces:**
- Consumes: `PublishSafetyValidator`, `CredentialResolver`, `INugetFeedClient`
- Produces: `IPackagePublisherService.PublishAsync(PublishPackageRequest request, CancellationToken ct) -> Task<PublishPackageResponse>`

- [ ] **Step 1: Write failing tests for publisher service**
  In `tests/PinkRooster.NugetPublisher.Tests/Publishing/PackagePublisherServiceTests.cs`:
  - `Publish_WhenPreflightFails_AbortsAndReturnsDiagnosticsWithoutPush`
  - `Publish_WhenValid_PushesNupkgThenSnupkgSequentially`
  - `Publish_WhenCompanionSnupkgMissing_PushesNupkgAndReturnsWarning` (Review Focus #2)
  - `Publish_WhenConflictAndSkipDuplicateTrue_ReturnsSuccessWithWarning`

- [ ] **Step 2: Run test to verify failure**
  Run: `dotnet test --filter "FullyQualifiedName~PackagePublisherServiceTests" -c Release`
  Expected: FAIL

- [ ] **Step 3: Implement `PackagePublisherService`**
  Implement the stateless guarded publishing workflow and symbol upload sequencing in `src/PinkRooster.NugetPublisher/Publishing/PackagePublisherService.cs`.

- [ ] **Step 4: Run test to verify passing**
  Run: `dotnet test --filter "FullyQualifiedName~PackagePublisherServiceTests" -c Release`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/PinkRooster.NugetPublisher/Publishing/ tests/PinkRooster.NugetPublisher.Tests/Publishing/
  git commit -m "feat: implement package publisher service with companion symbol sequencer"
  ```

---

### Task 7: Version Administration Service: Unlist & Relist

**Files:**
- Create: `src/PinkRooster.NugetPublisher/Administration/IPackageAdministrationService.cs`
- Create: `src/PinkRooster.NugetPublisher/Administration/PackageAdministrationService.cs`
- Create: `tests/PinkRooster.NugetPublisher.Tests/Administration/PackageAdministrationServiceTests.cs`

**Interfaces:**
- Consumes: `CredentialResolver`, `INugetFeedClient`
- Produces:
  - `IPackageAdministrationService.ManageVersionAsync(ManageVersionRequest request, CancellationToken ct) -> Task<ManageVersionResponse>`
  - `IPackageAdministrationService.GetDeprecationPortalUrl(DeprecationUrlRequest request) -> string`

- [ ] **Step 1: Write failing tests for version administration**
  In `tests/PinkRooster.NugetPublisher.Tests/Administration/PackageAdministrationServiceTests.cs`:
  - `Unlist_WhenValid_SendsDeleteAndReturnsUnlistedStatus`
  - `Relist_WhenValid_SendsPostAndReturnsListedStatus`
  - `Unlist_WhenDryRunTrue_QueriesFeedWithoutMutating`
  - `Unlist_WhenFeedReturns405MethodNotAllowed_ReturnsActionableGuidance` (Review Focus #4)
  - `GetDeprecationPortalUrl_GeneratesValidNugetOrgPortalLink`

- [ ] **Step 2: Run test to verify failure**
  Run: `dotnet test --filter "FullyQualifiedName~PackageAdministrationServiceTests" -c Release`
  Expected: FAIL

- [ ] **Step 3: Implement `PackageAdministrationService`**
  Implement unlist, relist, dry-run state queries, 405 feed exception handling, and deprecation portal URL generation.

- [ ] **Step 4: Run test to verify passing**
  Run: `dotnet test --filter "FullyQualifiedName~PackageAdministrationServiceTests" -c Release`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/PinkRooster.NugetPublisher/Administration/ tests/PinkRooster.NugetPublisher.Tests/Administration/
  git commit -m "feat: implement package version administration service"
  ```

---

### Task 8: Security Advisories & Vulnerability Audit Engine

**Files:**
- Create: `src/PinkRooster.NugetPublisher/Vulnerabilities/IVulnerabilityAuditService.cs`
- Create: `src/PinkRooster.NugetPublisher/Vulnerabilities/VulnerabilityAuditService.cs`
- Create: `tests/PinkRooster.NugetPublisher.Tests/Vulnerabilities/VulnerabilityAuditServiceTests.cs`

**Interfaces:**
- Consumes: `INugetFeedClient`
- Produces: `IVulnerabilityAuditService.AuditPackageAsync(string packageId, string? version, string? feedUrl, CancellationToken ct) -> Task<VulnerabilityAuditReport>`

- [ ] **Step 1: Write failing tests for vulnerability auditing**
  In `tests/PinkRooster.NugetPublisher.Tests/Vulnerabilities/VulnerabilityAuditServiceTests.cs`:
  - `AuditPackage_WhenVulnerabilitiesPresent_ReturnsParsedAdvisoriesAndSeverity`
  - `AuditPackage_WhenNoVulnerabilities_ReturnsCleanReport`

- [ ] **Step 2: Run test to verify failure**
  Run: `dotnet test --filter "FullyQualifiedName~VulnerabilityAuditServiceTests" -c Release`
  Expected: FAIL

- [ ] **Step 3: Implement `VulnerabilityAuditService`**
  Parse NuGet v3 registration vulnerability records and query public advisory repositories.

- [ ] **Step 4: Run test to verify passing**
  Run: `dotnet test --filter "FullyQualifiedName~VulnerabilityAuditServiceTests" -c Release`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/PinkRooster.NugetPublisher/Vulnerabilities/ tests/PinkRooster.NugetPublisher.Tests/Vulnerabilities/
  git commit -m "feat: implement vulnerability audit engine"
  ```

---

### Task 9: Stdio MCP Server Host & Tool Endpoint Registration

**Files:**
- Create: `src/PinkRooster.NugetPublisherMcp/Program.cs`
- Create: `src/PinkRooster.NugetPublisherMcp/Tools/NugetDiscoveryTools.cs`
- Create: `src/PinkRooster.NugetPublisherMcp/Tools/NugetInspectionTools.cs`
- Create: `src/PinkRooster.NugetPublisherMcp/Tools/NugetPublishingTools.cs`
- Create: `src/PinkRooster.NugetPublisherMcp/Tools/NugetAdministrationTools.cs`
- Create: `tests/PinkRooster.NugetPublisher.Tests/Tools/NugetMcpToolsTests.cs`

**Interfaces:**
- Consumes: `ModelContextProtocol`, All `PinkRooster.NugetPublisher` domain services
- Produces: Executable .NET tool configured with `<PackAsTool>true</PackAsTool>` exposing the 8 MCP tools over `stdio`.

- [ ] **Step 1: Write integration tests invoking tool delegates**
  In `tests/PinkRooster.NugetPublisher.Tests/Tools/NugetMcpToolsTests.cs`:
  - Test `nuget_search` delegate mapping
  - Test `nuget_publish` delegate mapping with pre-flight check
  - Test `nuget_manage_version` delegate mapping
  - Test secret masking in tool exception handler

- [ ] **Step 2: Run test to verify failure**
  Run: `dotnet test --filter "FullyQualifiedName~NugetMcpToolsTests" -c Release`
  Expected: FAIL

- [ ] **Step 3: Implement `Program.cs` and Tool Handlers**
  Configure MCP stdio server using `ModelContextProtocol` SDK, register services with DI, and define tool schemas.

- [ ] **Step 4: Run test to verify passing**
  Run: `dotnet test --filter "FullyQualifiedName~NugetMcpToolsTests" -c Release`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/PinkRooster.NugetPublisherMcp/ tests/PinkRooster.NugetPublisher.Tests/Tools/
  git commit -m "feat: implement stdio MCP server host and tool registrations"
  ```

---

### Task 10: Governance, Public API Snapshots, and PinkRooster.Check Quality Gate

**Files:**
- Create/Update: `tests/PinkRooster.NugetPublisher.Repository.Tests/*`
- Update: Public API snapshot files
- Update: `README.md`, `Directory.Build.props`

**Interfaces:**
- Consumes: Solution build outputs
- Produces: 0 findings from `dotnet run --project ~/Development/PinkRooster.Templates/tools/PinkRooster.Check -- .`

- [ ] **Step 1: Generate Public API snapshots for PinkRooster.NugetPublisher**
  Run:
  ```bash
  PINKROOSTER_UPDATE_PUBLIC_API=1 dotnet test --project tests/PinkRooster.NugetPublisherMcp.Repository.Tests
  ```

- [ ] **Step 2: Build, test, and pack solution in Release configuration**
  Run:
  ```bash
  dotnet build PinkRooster.NugetPublisherMcp.slnx -c Release
  dotnet test --solution PinkRooster.NugetPublisherMcp.slnx -c Release --no-build
  dotnet pack PinkRooster.NugetPublisherMcp.slnx -c Release --no-build -o artifacts
  ```
  Expected: All exit 0.

- [ ] **Step 3: Run PinkRooster.Check quality gate**
  Run:
  ```bash
  dotnet run --project /home/najgeetsrev/Development/PinkRooster.Templates/tools/PinkRooster.Check -- .
  ```
  Expected: Exit code 0 with 0 findings.

- [ ] **Step 4: Commit**
  ```bash
  git add -A
  git commit -m "chore: align repository with PinkRooster quality gates and snapshot public API"
  ```
