# Security Policy

## Supported versions

| Version | Supported |
| --- | --- |
| 0.1.x | yes |

## Reporting a vulnerability

Please **do not open a public issue**. Use GitHub Security Advisories ("Report a vulnerability") for this
repository. Include:

- Island version and Windows build;
- steps to reproduce;
- expected and actual behaviour;
- a minimal proof of concept if possible (no live secrets).

Do not include calendar URLs, tokens or personal files. We will acknowledge the report and coordinate a fix;
no response-time guarantee is made.

## What counts as a vulnerability

- code or command execution from external data (ICS, GitHub releases, API responses);
- bypassing the SHA-256 check during update;
- leaking tokens or calendar URLs into logs;
- writing files outside the data directory or the temporary directory.

## Project practices

- No secrets are stored in the repository. User data lives in `%APPDATA%\\Island`.
- Updates are downloaded over HTTPS, verified with SHA-256 from the release notes and installed only after
  explicit confirmation.
- Logs contain error messages and app paths, not calendar content or credentials.
- Release binaries are currently unsigned; verify the published SHA-256 before running them.
