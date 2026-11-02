# Privacy

## Summary

Island has **no telemetry and no analytics**. It stores its configuration and user content locally and contacts
only the services needed for the features you use.

## Local data

| Path | Contents |
| --- | --- |
| `%APPDATA%\\Island\\settings.json` | Settings, notes, reminders, weather/calendar caches, saved cities, volume presets, update repository name |
| `%APPDATA%\\Island\\error.log` | Runtime errors and failed settings/notes operations (no calendar content) |
| `%APPDATA%\\Island\\error.1.log` | Previous error log after rotation (above 1 MB) |

Set `ISLAND_DATA_DIR` to store everything in another folder. Delete the folder to remove all data.

## Network requests

| Destination | When | Data sent |
| --- | --- | --- |
| `geocoding-api.open-meteo.com` | City search | Search text |
| `api.open-meteo.com` | Weather refresh (primary) | City coordinates |
| `api.met.no` | Weather fallback when Open-Meteo is unreachable | City coordinates |
| `lrclib.net` | Lyrics toggle / track change | Artist, title, duration |
| Your ICS URL | Startup, every 15 minutes | Nothing (GET of your subscription) |
| `api.github.com` / GitHub release assets | Manual update check | Repository name, user agent |

All requests use HTTPS. There is no proxy, no third-party analytics and no crash reporting service.

## Calendar URL

The ICS URL is stored **in plain text** in `settings.json`. If your provider embeds a secret token in the URL,
treat the file as sensitive, do not share it, and prefer a read-only subscription link you can revoke.

## Logs

Logs contain exception types/messages and file paths of the app's own data directory. They do not contain
calendar event text, tokens or network credentials.

## Deletion

- Delete `%APPDATA%\\Island` to remove settings, notes and caches.
- Disable autostart before uninstalling (it writes a value under `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run`).

## Uncertainty

Island depends on third-party services (Open-Meteo, LRCLIB, GitHub, your calendar provider) whose own privacy
policies apply to requests they receive. This document describes Island's behaviour only; it was verified by
code inspection, not by network traffic analysis on every Windows configuration.
