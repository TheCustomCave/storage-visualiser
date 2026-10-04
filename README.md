# Storage Visualiser

A modern, portable, open-source disk-space visualiser for Windows, inspired by the clean nested-treemap layout of SpaceMonger 1.x.

> **Status:** pre-alpha. Scoping is done and the tech stack is chosen. The MVP is in progress.

## Goals
- **SpaceMonger-style treemap**: clean, nested and easy to read, with drill-down and back-up navigation.
- **Portable**: one folder, no installer, no prerequisites. Run it from USB or deploy it via RMM.
- **Secure**: no network access, read-only by default, standard user by default, code-signed releases.
- **Commercial-friendly**: MIT licensed, and every dependency is free for commercial use.
- **For everyone**: simple for normal users, with extra views, exports and a CLI for IT techs.

## Documentation
- [Scope & requirements](docs/scope.md)
- [Tech selection](docs/tech-selection.md)
- [Code signing & PKI plan](docs/code-signing.md)

## Tech stack
C# / .NET 10 · Avalonia UI · SkiaSharp · Native AOT (Windows x64)

## Licence
[MIT](LICENSE). Not affiliated with SpaceMonger, WinDirStat, WizTree or TreeSize.
