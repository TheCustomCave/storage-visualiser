# Storage Visualiser

[![Release](https://img.shields.io/github/v/release/TheCustomCave/storage-visualiser?color=blue&label=release)](https://github.com/TheCustomCave/storage-visualiser/releases)
[![Build & Test](https://github.com/TheCustomCave/storage-visualiser/actions/workflows/ci.yml/badge.svg)](https://github.com/TheCustomCave/storage-visualiser/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20x64-lightgrey.svg)]()
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)]()

A modern, ultra-fast, portable disk-space visualiser for Windows, inspired by the clean nested-treemap layout of **SpaceMonger 1.x**. Built for everyday users and IT technicians alike.

---

## ✨ Features

- **🗺️ SpaceMonger-Style Nested Treemap**: Clean, readable nested rectangular layout with folder headers, rainbow depth palette, `<Other>` grouping for tiny files, and `<Free Space>` representation on drive roots.
- **⚡ Dual-Engine High-Speed Scanner**:
  - **Directory Walker (Standard User)**: High-speed multi-threaded directory walker requiring zero administrative privileges (~16s across an entire 1.8 TB drive).
  - **NTFS MFT Direct Scanner (Elevated)**: High-performance raw Master File Table parser streaming clusters directly with USA fixup validation, scanning millions of files in **under 5 seconds**!
- **🌲 Multi-Tabbed Analysis**:
  - **Map**: Interactive treemap with smooth drill-down, hover tooltips, and real-time detail density slider.
  - **Tree**: Hierarchical directory tree with visual percentage progress bars (RidNacs-style).
  - **Top Files**: Sortable table ranking the 200 largest files on disk with direct shell integration.
  - **File Types**: Storage consumption breakdown sorted by file extensions and categories.
- **🛡️ Safe Delete to Recycle Bin**:
  - Seamless Windows Recycle Bin integration (`SHFileOperation` with undo support).
  - Protected paths blocklist preventing accidental deletion of critical system directories (`C:\Windows`, `Program Files`, drive roots, pagefile, hiberfil, etc.).
  - Confirmation dialog with full file metadata and local audit action logging (`file_actions.log`).
- **📊 100% Self-Contained Offline HTML Report**:
  - Export full interactive storage reports into a single standalone HTML5 file.
  - Features an offline canvas treemap, search filtering, breadcrumb navigation, and breakdown tables.
  - **Strictly offline**: Zero external scripts, remote fonts, or network telemetry.
- **🖱️ Explorer & Navigation Integration**:
  - **Reveal in Explorer**: Right-click any item across Map, Tree, or Top Files tabs to open its containing folder in Windows Explorer with the exact file or directory selected.
  - **Native File Properties**: Launch the native Windows shell properties dialog for any scanned file, folder, or drive.
  - **Drag and Drop Scanning**: Drag any folder or drive directly from Windows Explorer into the application to start scanning.
  - **Fluid Drill-Down Navigation**: Double-click to zoom into folders, interactive breadcrumb navigation bar, and full history traversal (`Back` / `Forward` / `Up` / `Home`).
- **♿ WCAG AAA Accessibility & Visual Polish**:
  - High-contrast text labels (>10:1 contrast ratio) over soft pastel data bars.
  - Pixel-perfect integer canvas rendering and balanced toolbar heights.

---

## 🚀 Quick Start

### Download & Run (No Installation Required)

1. Download **`StorageVisualiser-win-x64.zip`** from the [Latest Releases](https://github.com/TheCustomCave/storage-visualiser/releases).
2. Extract the zip to any folder (or run it directly from a USB stick).
3. Launch **`StorageVisualiser.exe`**.

> **Tip for IT Technicians:** Right-click and choose **"Run as administrator"** on local NTFS drives to enable the sub-second direct **NTFS MFT Direct Scanner**.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| <kbd>F5</kbd> | Rescan current drive or folder |
| <kbd>Backspace</kbd> / <kbd>Alt</kbd> + <kbd>↑</kbd> | Navigate to parent directory (Up) |
| <kbd>Alt</kbd> + <kbd>←</kbd> | Navigate Back in history |
| <kbd>Alt</kbd> + <kbd>→</kbd> | Navigate Forward in history |
| <kbd>Delete</kbd> | Move selected file or directory to Recycle Bin |
| <kbd>Double Click</kbd> | Zoom into folder / Open file in Explorer |

---

## 🔒 Security & Privacy Guiding Principles

- **Zero Network Access**: No telemetry, no automatic update pingbacks, no HTTP requests, and no external CDN dependencies. Safe for air-gapped systems and strict ThreatLocker allowlists.
- **Read-Only by Default**: File modifications are restricted to safe, user-confirmed Recycle Bin actions.
- **Cloud Placeholder Safe**: Strictly respects `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS` and never triggers hydration of OneDrive, Dropbox, or iCloud files during scans.
- **Symlink & Junction Safe**: Reparse points and volume mount points are recognized without traversing loops.
- **Commercial Friendly**: 100% [MIT Licensed](LICENSE). Every dependency adheres strictly to permissive commercial licenses (MIT / Apache-2.0 / BSD). See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

---

## 🛠️ Building from Source

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10/11 x64

### Build & Test
```powershell
# Clone the repository
git clone https://github.com/TheCustomCave/storage-visualiser.git
cd "storage-visualiser"

# Build solution
dotnet build -c Release

# Run automated tests (35+ unit tests)
dotnet test -c Release

# Publish portable single-file binary
dotnet publish src/StorageVisualiser.App/StorageVisualiser.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64
```

---

## 📖 Documentation

- [Requirements & Decisions Log](docs/scope.md)
- [Technology & Architecture Selection](docs/tech-selection.md)
- [Code Signing & PKI Plan](docs/code-signing.md)
- [Changelog](CHANGELOG.md)

---

## ⚖️ Licence

Distributed under the [MIT Licence](LICENSE).

*Disclaimer: Storage Visualiser is an independent open-source project and is not affiliated with or endorsed by SpaceMonger, WinDirStat, WizTree, or TreeSize.*
