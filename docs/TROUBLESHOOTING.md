# Troubleshooting

Diagnostics first: check `%APPDATA%\\Island\\error.log` (and `error.1.log`). Do not attach your
`settings.json` publicly — it contains your calendar URL and other personal data.

## The app does not start

**Symptoms:** nothing happens after `Island.exe`, or a .NET dialog appears.

1. Framework-dependent build: install the **.NET 10 Desktop Runtime** (x64), or use the self-contained build.
2. Check Windows version: Windows 10 2004 (19041) or newer is required.
3. Run from a folder where you have write access; the app writes to `%APPDATA%\\Island`.
4. Look at `error.log`. A startup XAML error usually indicates a corrupted installation — re-extract the archive.
5. If an older instance is still running, exit it from the tray; only one instance is allowed.

## The island is not visible or is in the wrong place

1. The island may be on another monitor: open **Settings → Monitor** and choose the primary display.
2. After a monitor change, the position is recalculated automatically; if not, change the dock position once.
3. The pill is always-on-top but transparent around the panel; move the cursor to the screen edge where it sits.
4. Check that the dock position in settings matches the edge you expect.

## The island overlaps the taskbar

The app uses the monitor work area and subtracts the taskbar strip even when auto-hide is enabled. If the
taskbar is on another monitor or has an unusual configuration, set the dock position to the opposite edge and
report the monitor/taskbar geometry.

## Live glass looks wrong (black, static or missing)

- Protected (DRM) content is captured as black by design.
- When capture is unavailable (secure desktop, remote session), the app falls back to the blurred wallpaper.
- If the glass is enabled, the island is excluded from screenshots and screen recordings
  (`WDA_EXCLUDEFROMCAPTURE`) — that is expected.
- Toggle **Live glass** off and on to force a re-initialization.
- Need the island in OBS/Discord? Enable **Show in screen capture** — live glass turns off and the
  wallpaper glass is used instead.

## Hotkey does not work

- Another application may own `Ctrl+Alt+Space`. Close it or use the overflow menu → Quick actions.
- The hotkey is registered per app instance; if registration failed (conflict), the menu still works.

## Notifications are silent

- Check **Quiet hours** in settings and Windows Focus Assist / Do Not Disturb.
- Notification sound is a setting (none/soft/bright/system); "none" disables sound.
- Reminder toasts need the app to be running.

## Music controls do not respond

- Island uses Windows media sessions; some players do not expose SMTC controls.
- Check that the player is playing and appears in the Windows media overlay.
- Lyrics require LRCLIB to know the track; "No lyrics" is a valid result, not an error.

## Weather does not load

- The app uses Open-Meteo and falls back to MET Norway when the forecast host is unreachable;
  a cached forecast is shown when offline.
- Check `error.log` for HTTP errors; corporate proxies may block the request.
- Use the refresh button or reopen the weather tab.

## Notes do not sync

- The sync folder must exist and be writable.
- Notes are exported on save and merged at startup; if two devices edit the same note, the newer
  `Updated` timestamp wins.
- Check `error.log` for "notes export/merge failed" entries.

## High CPU usage

- Live glass captures the screen ~14 times per second while expanded; collapsing the island stops it.
- The visualizer runs only while the Music tab is open and stops on battery.
- If CPU stays high while collapsed, check for a stuck toast/sticky window and restart the app.

## Settings are not saved

- Check that `%APPDATA%\\Island` is writable.
- Errors during save are written to `error.log`; the app keeps running with in-memory settings.
- A corrupted `settings.json` is ignored and defaults are used; restore from a backup if available.

## Build errors

- Use .NET SDK 10.0 (`dotnet --version`).
- Run `dotnet build src/Island/Island.csproj` to see the full error.
- If a build reports a locked `Island.exe`, close the running app first.
- Tests: `dotnet run --project tests/ParserTests/ParserTests.csproj`.

## What to include in a bug report

- Island version and Windows build (`winver`).
- Monitor layout: count, resolutions, scaling, taskbar position and auto-hide.
- The relevant part of `error.log` — **redact** calendar URLs, tokens and personal text.
- Steps to reproduce and what you expected.
