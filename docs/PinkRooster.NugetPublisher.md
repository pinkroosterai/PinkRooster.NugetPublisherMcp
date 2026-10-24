<img src="https://raw.githubusercontent.com/pinkroosterai/PinkRooster/a3815ce62a2491a8ee1c7401e1e48aa43ef20a85/assets/logo.png" alt="PinkRooster logo" width="120" />

# PinkRooster.NugetPublisher

Core library for NuGet package inspection, validation, credential resolution, and publishing.

Status: preview, developed against the .NET 10 SDK.

```text
dotnet add package PinkRooster.NugetPublisher --prerelease
```

## Quickstart

```csharp
AreaInfo info = new();
```

## Overview

PinkRooster.NugetPublisher provides core capabilities for .NET package management:
- Local nupkg archive and nuspec inspection
- NuGet v3 feed client supporting search, metadata, and push endpoints
- Multi-source credential resolution with automatic secret sanitization
- Pre-flight publish safety validation
- Package publisher with companion symbol sequencer
- Vulnerability auditing via NuGet v3 vulnerability metadata
- Package version administration and deprecation guidance

## Compatibility

.NET 10. No dependencies besides the NuGet client libraries.
