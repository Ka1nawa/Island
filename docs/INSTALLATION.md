# Installation

## Requirements

| Component | Requirement |
| --- | --- |
| OS | Windows 10 version 2004 (build 19041) or newer; Windows 11 |
| Architecture | x64 |
| Runtime | .NET 10 Desktop Runtime (framework-dependent build only) |
| Display | Any; per-monitor DPI is handled by the app |

## Choosing a build

| | Framework-dependent | Self-contained |
| --- | --- | --- |
| File size | ~26 MB | ~181 MB |
| Required runtime | .NET 10 **Desktop** Runtime (x64) | None |
| Best for | machines that already have .NET 10 | clean machines, portable use |
| Update size | smaller | larger |

Both archives contain a single `Island.exe` (single-file publish). The self-contained build includes the
.NET runtime and needs nothing else.

## Install from a release artifact

1. Download one of:
   - `Island-selfcontained-0.1.2.exe` — recommended, no runtime required (~181 MB);
   - `Island-framework-0.1.2.exe` — smaller (~26 MB), requires the .NET 10 Desktop Runtime.
2. Verify the SHA-256 from the release notes if provided.
3. Put the file in a folder of your choice, for example `%LOCALAPPDATA%\\Programs\\Island`.
4. Run the downloaded `.exe`. The island appears at the top centre of the primary display.
5. Optionally enable **Autostart** in settings or the tray menu.

The binaries are not code-signed. Windows SmartScreen may show a warning; choose "More info" → "Run anyway"
only if you trust the source and verified the hash.

## Build from source

Requirements: .NET SDK 10.0, Git.

```powershell
git clone <repository-url>
cd island
dotnet build src/Island/Island.csproj
.\Island.cmd
```

`Island.cmd` runs `run.ps1`, which rebuilds when sources are newer than the executable and starts it.

### Release build

```powershell
# framework-dependent, single file
dotnet publish src/Island/Island.csproj -c Release -r win-x64 --self-contained false \
  -p:PublishSingleFile=true -o publish/win-x64

# self-contained, single file
dotnet publish src/Island/Island.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64-sc
```

## Data location

- Settings, notes, reminders and caches: `%APPDATA%\\Island\\settings.json`
- Errors: `%APPDATA%\\Island\\error.log`; rotated to `error.1.log` above 1 MB.
- Override the data directory with the environment variable `ISLAND_DATA_DIR` (portable mode).

## Update

Island checks GitHub Releases when **Settings → Update repository** (`owner/repo`) is configured.
An update is downloaded only after confirmation, and only if the release notes contain `sha256: <hash>`;
the file is verified before the installer is offered.

## Uninstall

1. Disable autostart (tray menu or settings).
2. Exit Island from the tray.
3. Delete the downloaded .exe.
4. Optionally delete `%APPDATA%\\Island` to remove all settings, notes and caches.

## License

Island is distributed under the **MIT License**. See [LICENSE](../LICENSE) for the full text.

## Common installation problems

- **"You must install .NET"** — install the .NET 10 Desktop Runtime or use the self-contained build.
- **SmartScreen warning** — expected for an unsigned build; verify the SHA-256 first.
- **App starts but no island is visible** — the island may be off-screen after a monitor change; see
  [TROUBLESHOOTING.md](TROUBLESHOOTING.md).
