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

Say in one to three paragraphs which problem PinkRooster.NugetPublisherMcp solves and for whom.

## Install

```text
dotnet add package PinkRooster.NugetPublisherMcp --prerelease
```

## Quickstart

<!-- quickstart:start -->
```csharp
Greeter greeter = new();
string greeting = greeter.Greet("world");
```
<!-- quickstart:end -->

## Packages

| Package | What it adds | Dependencies |
|---|---|---|
| [`PinkRooster.NugetPublisherMcp`](https://www.nuget.org/packages/PinkRooster.NugetPublisherMcp) | Production-grade Model Context Protocol (MCP) server for NuGet package inspection, publishing, auditing, and administration. | none |

## Samples

See [samples/README.md](samples/README.md).

## Docs

- [PinkRooster.NugetPublisherMcp](docs/PinkRooster.NugetPublisherMcp.md)

## Compatibility

.NET 10 SDK; tested on Ubuntu and Windows.

## Build and test

```text
dotnet build PinkRooster.NugetPublisherMcp.slnx -c Release
dotnet test --solution PinkRooster.NugetPublisherMcp.slnx -c Release
```

## Feedback, contributing, security and license

Read [CONTRIBUTING.md](CONTRIBUTING.md) first. Report a vulnerability as [SECURITY.md](SECURITY.md) describes. The code is [MIT](LICENSE) licensed.
