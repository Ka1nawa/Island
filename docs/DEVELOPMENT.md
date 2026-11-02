# Development guide

## Environment

- Windows 10 version 2004+ or Windows 11, x64.
- .NET SDK **10.0** (`dotnet --version` → 10.0.x).
- Visual Studio 2022+ or VS Code with the C# extension (optional).
- Git (optional).

No other SDKs or package managers are required.

## Build, run, test

```powershell
dotnet build src/Island/Island.csproj
dotnet run --project tests/ParserTests/ParserTests.csproj
.\Island.cmd
```

`Island.cmd` starts `run.ps1`, which rebuilds if any `.cs`/`.xaml` is newer than the executable, then launches it.
There is no linter or formatter configured in the repository; keep the existing style and run a build with
warnings enabled before committing.

## Project layout

```
island/
  Island.cmd, run.ps1          start scripts
  README.md, CHANGELOG.md      project docs
  docs/                        user and developer documentation
  shots/                       screenshots used by the docs
  src/Island/
    App.xaml(.cs)              resources, startup, single instance, log rotation
    MainWindow.xaml(.cs)       island shell, glass, tabs, overlays, tray, hotkey, toasts, stickies
    ToastWindow.xaml(.cs)      notification card
    StickyWindow.xaml(.cs)     desktop sticky note
    Theme.cs, Localization.cs  theme and string switching
    Controls/                  MarkdownText, VisualizerControl, WeatherIcon
    Converters/                small XAML converters
    Views/                     MediaPage, SoundPage, WeatherPage, PlansPage, SettingsSheet, AboutSheet, CommandSheet, SystemPage
    ViewModels/                Main, Media, Sound, Weather, Plans, Settings, System
    Services/                  media, audio, spectrum, weather, glass, live glass, calendar, lyrics,
                               reminders, timer, settings, notifications, toast, updates, hardware, autostart, notes sync
    Resources/                 Strings.ru/en.xaml, Theme.Dark/Light.xaml
  tests/ParserTests/           console test runner (no external test framework)
```

## Architecture in one paragraph

`MainWindow` owns the island UI and orchestrates everything. It creates the view models, wires services and
runs a 250 ms `DispatcherTimer` that updates the clock, media view model, equalizer and periodic refreshes.
View models hold state and expose properties to XAML; services talk to Windows and the network and never
reference UI types (except the glass image, which is a WPF `BitmapSource`). See [ARCHITECTURE.md](ARCHITECTURE.md).

## Conventions

- C# latest, nullable enabled, file-scoped namespaces, `sealed` classes, explicit types where they help.
- UI text goes through `L.T("Key")` / `L.F("Key", args)`; add the key to **both** `Strings.ru.xaml` and
  `Strings.en.xaml`.
- Colours come from theme brushes (`DynamicResource TextPrimary`, `SurfaceSoft`, `BorderSoft`, …) or from
  `Theme.Dark/Light.xaml`; avoid new hard-coded colours that break the light theme.
- Services catch their own failures, log through `SettingsStore.Dir` (`error.log`) and return safe defaults;
  never let a background failure crash the UI thread.
- Async work uses `async/await` and `ConfigureAwait` is not required (WPF synchronization context).
- No secrets, tokens, local user paths or personal data in code, tests, docs or screenshots.

## Adding a feature

1. Add or extend a service in `src/Island/Services`. Keep it UI-free; return plain data.
2. Expose state through a view model (`ObservableObject` from `Core`).
3. Build the UI in a `Views/*.xaml` page or a tab in `MainWindow.xaml`; bind to the view model.
4. Wire it in `MainWindow` (constructor, timer, cleanup in `OnClosing`).
5. Add strings to both dictionaries; add theme-aware colours.
6. Add tests for pure logic in `tests/ParserTests/Program.cs` (pattern: parse/merge/validate functions).
7. Document user-visible behaviour in `docs/USER_GUIDE.md` and the changelog.

## Settings and migrations

`AppSettings` in `Services/SettingsStore.cs` is serialized to `settings.json`. Saving is atomic
(temp file + `File.Replace`, previous file kept as backup on replace). When adding a property:

1. Add it with a sensible default.
2. Add a migration step in `SettingsStore.Migrate` if old files need a value.
3. Expose it in `SettingsViewModel` (save on set, raise an event if the window must react).
4. Add the control in `Views/SettingsSheet.xaml`.

## Themes and localisation

- `Theme.Apply("dark"|"light"|"auto")` replaces the merged theme dictionary at runtime; use `DynamicResource`
  for anything that must change immediately.
- `L.Apply("ru"|"en")` replaces the strings dictionary; `DynamicResource` in XAML updates live.

## Release build and versioning

- Version lives in `src/Island/Island.csproj` (`Version`, `AssemblyVersion`, `FileVersion`).
- Update `CHANGELOG.md` and `RELEASE_NOTES_DRAFT.md` for user-visible changes.
- Publish commands are in [INSTALLATION.md](INSTALLATION.md#release-build).
- Bump the version, rebuild, run the tests, re-publish and compute SHA-256 for each artifact.

## Before a pull request

```powershell
dotnet build src/Island/Island.csproj
dotnet run --project tests/ParserTests/ParserTests.csproj
dotnet list src/Island/Island.csproj package --vulnerable --include-transitive
```

Check that the app starts, the island expands/collapses, settings persist after a restart, and the affected
feature works in both themes. See [CONTRIBUTING.md](../CONTRIBUTING.md).
