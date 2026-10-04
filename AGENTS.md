# AGENTS.md: rules for AI agents working in this repo

## Read first
1. `docs/scope.md`: requirements and the **decisions log** (D1…). Do not contradict a decision without flagging it.
2. `docs/tech-selection.md`: stack and architecture.
3. `docs/code-signing.md`: release signing plan.

## Non-negotiables
- **No network access** in the product. No telemetry, update checks, HTTP clients or remote fonts/scripts (the HTML report included).
- **Licences:** only MIT / BSD / Apache-2.0 / MS-PL dependencies. No GPL/AGPL/LGPL or "free for non-commercial" packages. Add every new dependency to `THIRD-PARTY-NOTICES.md`.
- **Read-only by default.** Any destructive file action must go through the central `FileActions` service, which enforces policy, protected paths, confirmation and the action log.
- **Never hydrate cloud placeholders.** Respect `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS` / `RECALL_ON_OPEN` / `OFFLINE`; never open file contents during scans.
- **Don't follow reparse points** (junctions/symlinks) during scans.
- **Snapshots and saved files are untrusted input.** Validate versions and sizes, and parse strictly.
- **Secrets:** never commit keys, certificates (`.pfx`) or tokens.
- `StorageVisualiser.Core` must not reference any UI framework.
- Keep it **Native AOT compatible**: no runtime reflection-based serialisation (use System.Text.Json source generators), and no dynamic code.

## Engineering conventions
- C# latest, nullable enabled, warnings as errors, analysers on.
- Add unit tests for the layout, scanner logic, policy, exports and snapshot parsing.
- Strings shown to users go in resource files (to make localisation possible later).
- Update `docs/scope.md` (decisions log) when behaviour changes, and `CHANGELOG.md` for every user-visible change.
