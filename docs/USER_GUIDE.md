# User guide

## What Island is

Island is a small always-on-top widget that lives as a pill at the screen edge. Hover it and it expands into a
panel with music, sound mixer, weather, notes/reminders, a system monitor and quick actions. Moving the cursor
away collapses it after 0.75 seconds.

## Requirements

- Windows 10 version 2004 (build 19041) or newer, or Windows 11, x64.
- .NET 10 Desktop Runtime for the framework-dependent build; nothing extra for the self-contained build.

## First launch

1. Run `Island.exe`. The island appears at the top centre of the primary monitor.
2. Hover the pill to expand it.
3. Open **Settings** (gear icon) to choose language, theme, time zone, monitor and dock position.
4. Optional: enable **Autostart** in settings or in the tray menu.

The app does not appear in the taskbar or Alt+Tab. A tray icon is created at startup.

## Interface

### Collapsed pill

- Album art, track title and equalizer while media is playing; clock and date otherwise.
- Current temperature on the right; click it to open the weather tab.
- A timer chip replaces the equalizer while a timer is running; click it to cancel.
- A meeting hint appears in the header when an event starts within an hour.

### Expanded panel

Tabs at the top:

| Tab | Contents |
| --- | --- |
| Music | Artwork, title/artist, seek bar, play/pause, previous/next, master volume, visualizer, **Lyrics** toggle |
| Sound | Output device selector, game/work volume presets, per-application volume and mute, system sounds |
| Weather | Current conditions, wind/humidity, hourly forecast, 5-day forecast, city search and city switch arrows |
| Plans | Notes and reminders (see below) |
| System | CPU, GPU and RAM usage |

Header buttons: settings (gear), overflow menu (quick actions, settings, about, check updates, refresh weather,
collapse, exit) and collapse.

### Notes and reminders (Plans)

- **Notes tab**: sticky-style cards. Open a card to edit title, body and colour, pin it to the desktop,
  add a reminder, or delete it.
- **Checklists**: write `- [ ]` / `- [x]` lines; the card shows checkboxes and toggles them in place.
- **Markdown**: headings (`# `), bold (`**text**`), italic (`*text*`), inline code and bullets are
  rendered in the preview.
- **Reminders tab**: create a reminder with a title, time, repeat rule (once, hourly, daily, weekly with
  weekdays) and an optional quick phrase.
- **Quick phrases**: type "in 20 minutes", "every hour", "on Wednesday at 15:00", "tomorrow at 10" (or the
  Russian equivalents) and press Enter to fill the fields; a preview shows the parsed schedule before saving.
- **Presets**: Water, Stretch and Duolingo fill the editor; adjust and save.
- **Export/import**: buttons in the notes header.
- **Folder sync**: set a folder in settings (for example a OneDrive folder). Notes are exported on save and merged
  at startup; for equal IDs the note with the newer update time wins.

### Desktop sticky notes

Enable **On desktop** in the note editor. A small always-on-top card appears; drag it by the header.
The close button unpins it. Positions are stored in settings and restored on the next launch.

## Settings

| Setting | Effect |
| --- | --- |
| Theme | Dark, Light or System (follows the Windows app theme) |
| Language | Russian or English, applied immediately |
| Time zone | System time or any time zone; clock and date follow it |
| Position | Six dock positions (top/bottom × left/centre/right) |
| Monitor | Primary display, display under the cursor, or a specific monitor |
| Background density | Glass tint opacity |
| Live glass | Blur the real desktop content behind the panel |
| Accent from artwork | Tint the glass and equalizer with the album colour |
| Animation speed | 0.5×–2× |
| Reduce motion | Short animations |
| Music visualizer | Enable or disable the spectrum |
| Quiet hours | No notification sound in the configured interval (supports overnight ranges) |
| Notification sound | None, soft, bright or system |
| Autostart | Start Island when you sign in (HKCU Run entry) |
| Calendar: ICS link | Your calendar subscription (webcal:// or https://); empty disables the calendar |
| Notes sync folder | Folder used for note synchronization |
| Update repository | GitHub `owner/repo` used for update checks |

## Keyboard and mouse

| Action | Result |
| --- | --- |
| Hover the pill | Expand |
| Move the cursor away | Collapse after 0.75 s |
| `Ctrl+Alt+Space` | Open quick actions; press again to close |
| `Esc` | Close the overlay/editor or collapse |
| Click the temperature | Open the weather tab |
| Click the timer chip | Cancel the timer |
| Double-click the tray icon | Show the island |

### Quick actions

Press `Ctrl+Alt+Space` (or use the overflow menu) and type:

- a schedule phrase such as "in 20 minutes" → creates a reminder at that time;
- a timer phrase such as "timer 25" or "25 min" → starts a 25-minute timer;
- anything else → creates a note with that text.

Press Enter to run, or use the Note/Reminder buttons.

## Notifications

Reminders and completed timers show a toast below the island with **Done** and snooze buttons
(5, 10 or 30 minutes; timers have no snooze buttons). The toast does not steal focus.
Quiet hours and Windows Focus Assist suppress the sound; the card is still shown.
Calendar events show an informational toast 15 minutes before the start.

## Music and lyrics

- Controls use the Windows media session (SMTC), so the app follows whatever plays system-wide.
- **Lyrics** loads synced or plain text from LRCLIB; if no lyrics exist, the panel says so.
- **Output device** switches the default playback device (console, multimedia and communications roles).
- **Game/Work presets** store the master volume and per-application levels; the disc icon saves the current
  state, the chip applies it.

## Weather

Open-Meteo is used for geocoding and forecast (no API key). The 5-day row shows max/min and precipitation
probability. A warning chip appears for thunderstorms, strong wind (40+ km/h) or heavy rain.
Use the arrows next to the city name to switch between saved cities.

## Calendar

Paste an ICS URL in settings. Island parses events, repeats (daily, weekly with weekdays, monthly), exceptions
and cancellations, and shows the next event in the header and a toast 15 minutes before it starts.
The calendar is read-only and needs no account.

## System tab

CPU (`GetSystemTimes`), GPU (`GPU Engine` performance counters) and RAM (`GlobalMemoryStatusEx`) are sampled
once per second while the tab is open. If GPU counters are unavailable, the tab shows "n/a".

## Data, restart and closing

- Settings, notes, reminders and caches are stored in `%APPDATA%\\Island\\settings.json`.
- Errors are written to `%APPDATA%\\Island\\error.log` (rotated to `error.1.log` above 1 MB).
- Settings are saved immediately and written atomically; unsaved changes are not lost on crash because each
  change is saved as it happens.
- Closing the app (overflow menu → Exit, or the tray menu) stops timers, closes sticky notes and saves settings.
- Only one instance can run at a time.

## Portable mode

Set the environment variable `ISLAND_DATA_DIR` to a folder before launching to keep all data next to the
application (for example on a USB drive).
