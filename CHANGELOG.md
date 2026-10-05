# Changelog

All notable changes to Storage Visualiser are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Tabbed Views Interface (v1)**:
  - **🗺️ Map**: SpaceMonger nested treemap with dynamic rainbow palette, free space display toggle, and detail density slider.
  - **🌲 Tree**: RidNacs-style hierarchical directory tree with visual percentage progress bars, formatted sizes, file counts, and descending size sort.
  - **📄 Top Files**: Sortable table displaying the top 200 largest files with size, percentage of total storage, file extension, full path, and date modified. Right-click or double-click to reveal in Windows Explorer.
  - **📊 File Types**: Extension and category breakdown table showing total consumed space, percentage of total, and file count per extension.
- **Cross-View Navigation**:
  - Context menu item **"Show on Map"** in Tree and Top Files tabs to quickly zoom to and highlight items in the treemap.
  - Full Windows Explorer shell integration (Open in Explorer, Open File, Copy Path, File Properties) across all tabs.
- **Safe Delete to Recycle Bin (v1)**:
  - Central `FileActionService` enforcing policy, protected paths blocklist, confirmation modal, and audit action logging (`file_actions.log`).
  - Native Windows Recycle Bin integration (`SHFileOperation` with `FOF_ALLOWUNDO`) ensuring all deleted files/directories can be restored.
  - Protected paths blocklist preventing accidental deletion of Windows directories, Program Files, ProgramData, drive roots, pagefile, hiberfil, swapfile, and synthetic items.
  - In-app confirmation modal showing file details, size, and warning before deletion.
  - Delete key shortcut and context menu items across Map, Tree, and Top Files tabs.
- **Accessibility & Contrast (WCAG AAA)**:
  - Updated progress bars in Tree, Top Files, and File Types tabs with soft pastel fills and deep navy text (`#0F172A`) providing >10:1 contrast ratios.
- **UI Refinements**:
  - Vertically centered text in the address bar TextBox.
  - Locked Row 2 navigation bar to exact fixed height to eliminate jitter when scanning starts or stops.
  - Balanced Back and Forward button widths with centered text.
- **Interactive Offline HTML Export (v1)**:
  - Single-file, 100% self-contained HTML5 report with zero external CDN scripts, remote fonts, or network requests (strict offline privacy).
  - Interactive canvas treemap with breadcrumb navigation, interactive click & drill-down, hover tooltips, and real-time detail slider.
  - Built-in search filter across scanned nodes, interactive Top Files table, and File Types breakdown.
  - Native AOT compatible JSON serialization via source-generated `ReportJsonContext`.
  - Windows file picker default naming format (`Storage Report - <HOST> - <Target> - <ISO 8601>.html`) defaulting to Documents directory.
- **Elevated NTFS MFT Scanner (v1 / S8)**:
  - High-speed direct Master File Table (`$MFT`) parser for local NTFS drive volumes when running with Administrator privileges.
  - Sequential multi-megabyte cluster run streaming via Win32 volume handle reading, parsing 1024-byte MFT records with fixup validation (Update Sequence Array), resident/non-resident `$DATA` streams, and `$FILE_NAME` attributes.
  - Seamless `WindowsAutoScanner` orchestrator: automatically selects high-speed MFT direct reading when elevated on local NTFS drives, and seamlessly falls back to standard `DirectoryWalkerScanner` when non-elevated, on network shares, or for subfolder scans.
  - Real-time active scanner indicator in the status bar (e.g. `Scan complete in 1.4s via NTFS MFT Direct Scanner`).
- **Elevated NTFS MFT Scanner Fixes & Diagnostics**:
  - Added `FILE_SHARE_DELETE` (0x04) and `FILE_FLAG_BACKUP_SEMANTICS` to raw volume `CreateFile` calls, preventing `ERROR_SHARING_VIOLATION` (32) when reading live operating system drives (like `C:`) with running background services.
  - Enabled `SeBackupPrivilege` process token privilege dynamically to guarantee unrestricted raw volume read access when elevated.
  - Fixed non-resident `$DATA` cluster run parsing in `NtfsMftRecordParser` for Record 0 (`$MFT`), resolving fallback caused by unextracted MFT cluster runs.
  - Enforced 4096-byte sector-aligned buffer allocations for Record 0 and volume reads, preventing `ERROR_INVALID_PARAMETER` (87) on modern Advanced Format (4Kn / 512e) NVMe SSDs.
  - Added sparse cluster run handling in `DataRunDecoder`.
  - Added comprehensive diagnostic logging from `WindowsAutoScanner` and `WindowsNtfsMftScanner` into `startup.log`.
- **Toolbar & Navigation Sizing & Alignment**:
  - Standardized all top toolbar buttons (`[Scan Drive]`, `[Browse...]`, `[Rescan]`, `[Export Report...]`, and `[Free Space]`) to an exact uniform height of 32px with centered vertical content alignment and consistent 4px corner radius, resolving button height unevenness caused by font weight variations.
  - Expanded Drive Selector ComboBox width from 230px to 285px and locked its height to 32px to eliminate clipping of volume labels and free space information (`... free of 1.82 TB`).
  - Standardized all navigation bar buttons (`◀ Back`, `▶ Forward`, `▲ Up`, `⌂ Home`, `[Cancel Scan]`, and address bar TextBox) to an exact uniform height of 30px with centered vertical content alignment.
  - Fixed Detail Slider thumb clipping and vertical misalignment by overriding FluentTheme slider dimensions (`SliderPreContentMargin` and `SliderPostContentMargin` set to 0, track height 20px, thumb diameter 14px/16px) to perfectly center with `Detail:` text and prevent thumb clipping against toolbar borders.
- **Unit Tests**:
  - Expanded test suite to 35 unit tests with coverage for MFT sparse run decoding and Record 0 non-resident `$DATA` cluster run parsing.
