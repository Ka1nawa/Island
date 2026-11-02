# Installation

## Requirements

| Component | Requirement |
| --- | --- |
| OS | Windows 10 version 2004 (build 19041) or newer; Windows 11 |
| Architecture | x64 |
| Runtime | .NET 10 Desktop Runtime (framework-dependent build only) |
| Display | Any; per-monitor DPI is handled by the app |

The self-contained build includes the .NET runtime and needs nothing else.

## Install from a release artifact

1. Download one of:
   - `Island-0.1.0-win-x64-selfcontained.zip` — recommended, no runtime required (~73 MB);
   - `Island-0.1.0-win-x64-framework.zip` — smaller (~7 MB), requires the .NET 10 Desktop Runtime.
2. Verify the SHA-256 from the release notes if provided.
3. Extract the archive to a folder of your choice, for example `%LOCALAPPDATA%\\Programs\\Island`.
4. Run `Island.exe`. The island appears at the top centre of the primary display.
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
3. Delete the application folder.
4. Optionally delete `%APPDATA%\\Island` to remove all settings, notes and caches.

## Common installation problems

- **"You must install .NET"** — install the .NET 10 Desktop Runtime or use the self-contained build.
- **SmartScreen warning** — expected for an unsigned build; verify the SHA-256 first.
- **App starts but no island is visible** — the island may be off-screen after a monitor change; see
  [TROUBLESHOOTING.md](TROUBLESHOOTING.md).
