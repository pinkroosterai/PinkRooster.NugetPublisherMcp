# Contributing

Bug reports and focused pull requests are welcome. Search existing issues before opening a new one. For a behavior change, describe the expected behavior and how it differs from the current one. Do not include API keys, private state, or live responses in issues or fixtures.

Keep changes close to the existing code. Add tests when changing behavior; they are xunit v3 tests, one project per package under `tests/`. Run these commands from the repository root before a pull request:

```text
dotnet build PinkRooster.NugetPublisherMcp.slnx -c Release
dotnet test --solution PinkRooster.NugetPublisherMcp.slnx -c Release
```

Warnings are errors; in `src` that includes a public member without an XML doc. The public API of each package is listed in `tests/PinkRooster.NugetPublisherMcp.Repository.Tests/PublicApi/` and a test compares it: after a change to the public API, run the tests once with `PINKROOSTER_UPDATE_PUBLIC_API=1` and commit the changed files. Use a Conventional Commit subject such as `fix: handle an empty name`. In a pull request, explain the behavior change, list the checks run, and link a related issue when there is one. Security issues belong in the private reporting channel described in [SECURITY.md](SECURITY.md).
