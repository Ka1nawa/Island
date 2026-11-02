# FAQ

**What is Island?**
A Windows desktop widget: a pill at the screen edge that expands on hover into a panel with music, weather,
notes/reminders, calendar, quick actions and system metrics.

**Is it free?**
The project has no license file yet. Until the owner adds one, it is not licensed for redistribution or
modification.

**Which Windows versions are supported?**
Windows 10 version 2004 (build 19041) and newer, including Windows 11. x64 only.

**Do I need .NET?**
Only for the framework-dependent build (download the .NET 10 Desktop Runtime). The self-contained build includes
everything.

**Does it work without internet?**
Yes. Weather falls back to the last cached forecast and the calendar to cached events; everything else is local.
Lyrics and update checks need internet.

**Does Island collect telemetry?**
No. There is no analytics or telemetry. See [PRIVACY.md](PRIVACY.md).

**Where are my settings?**
`%APPDATA%\\Island\\settings.json`. Use `ISLAND_DATA_DIR` for a portable setup. Delete the folder to reset.

**How do I move it to another monitor?**
Settings → Monitor: primary, under the cursor, or a specific display. The position is recalculated on display changes.

**Why does the island disappear from screenshots?**
When Live glass is enabled the window is excluded from capture so it cannot capture itself. Disable Live glass
to make it appear in screenshots.

**Why is there no screenshot/recording indicator?**
Windows has no public API to detect screen recording reliably, so Island does not show one.

**Can I change the hotkey?**
Not yet; `Ctrl+Alt+Space` is fixed. The same actions are available from the overflow menu.

**How do I uninstall?**
Exit from the tray, delete the application folder and optionally `%APPDATA%\\Island`. Disable autostart first.

**Why does SmartScreen warn?**
The binaries are not code-signed. Verify the SHA-256 and only run builds from a source you trust.

**Where are the logs?**
`%APPDATA%\\Island\\error.log`; rotated to `error.1.log` above 1 MB. Logs never include calendar URLs.
