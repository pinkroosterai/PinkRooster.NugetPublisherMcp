# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-preview.2]

### Fixed

- Replaced raw HTML `<img>` tag in packed package readmes with standard Markdown image syntax (`![PinkRooster logo](...)`) referencing a crisp 120px logo hosted on GitHub, ensuring proper rendering on NuGet.org where raw HTML tags are sanitized.

## [0.1.0-preview.1]

### Added

- Initial preview release of `PinkRooster.NugetPublisher` and `PinkRooster.NugetPublisherMcp`.
- **PinkRooster.NugetPublisher**: Core engine for package inspection, validation, credential resolution, version management, and publishing.
- **PinkRooster.NugetPublisherMcp**: Production-grade Model Context Protocol (MCP) server for autonomous AI coding agents (Antigravity, Claude Code, Cursor, Copilot).
- 9 granular MCP tools: `nuget_search`, `nuget_list_published`, `nuget_get_metadata`, `nuget_inspect_local`, `nuget_validate_publish`, `nuget_audit_vulnerabilities`, `nuget_manage_version`, `nuget_get_deprecation_url`, and `nuget_publish`.
- Secret sanitization protecting tokens in agent conversation transcripts.
- Strict pre-flight safety gates preventing accidental version collisions.
