# Contributing

Thanks for helping improve Island.

## Before you start

- Windows 10 2004+ / Windows 11, x64.
- .NET SDK 10.0.
- Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Build and test

```powershell
dotnet build src/Island/Island.csproj
dotnet run --project tests/ParserTests/ParserTests.csproj
```

## Branching and commits

The repository does not enforce a branching model. Keep changes focused, write commit messages in the
imperative mood, and do not mix formatting-only changes with behaviour changes.

## Pull requests

- Describe the problem and the fix; link an issue when one exists.
- Include a test for pure logic when possible (`tests/ParserTests/Program.cs`).
- Update the docs (`README.md`, `docs/`, `CHANGELOG.md`) when behaviour changes.
- Verify the app starts, expands/collapses, settings persist and the affected feature works in both themes.
- Do not include secrets, tokens, personal paths, calendar URLs or screenshots with a personal desktop.

## Issues

- Bugs: version, Windows build, monitor/DPI layout, steps to reproduce, relevant `error.log` excerpt
  (redact personal data).
- Feature requests: describe the use case, not only the UI.

## Security

Do not report vulnerabilities in public issues — see [SECURITY.md](SECURITY.md).
