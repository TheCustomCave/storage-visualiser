# Changelog

All notable changes to Storage Visualiser are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1-alpha] - 2026-10-05

### Added
- **File Type Filtering Across Map, Tree, and Top Files (GitHub #3)**:
  - Added context menu actions to the File Types tab: **"Filter Map & Tree by this Type"**, **"Filter Tree by this Type"**, and **"Show Top Files for this Type"**. Double-clicking any file type row immediately filters the Map and Tree views.
  - Added a responsive **Active Filter Indicator Banner** across the application displaying the active filter (`*.ext`), matching file count, total size, and a one-click **"✕ Clear Filter"** button.
  - Tree view shows only folders containing matching files (with matching counts/sizes) and the matching files themselves.
  - Treemap dynamically resizes folders and files according to their matching extension sizes.
  - Top Files tab displays the largest files matching the active filter.
- **HTML Report File Types Interaction (GitHub #3)**:
  - Clicking any row in the HTML report File Types table automatically switches to the Top Files tab and filters the search by that file extension.

### Fixed
- **NTFS MFT Scanner Directory Count Zero (GitHub #1)**:
  - Fixed live scan progress reporting during elevated NTFS MFT scans where directory count was hardcoded to 0 while file count incremented. Live counters now track both files and directories simultaneously.
- **HTML Report "Up" Navigation (GitHub #2)**:
  - Resolved an issue where pressing "Up" in the HTML report treemap returned all the way to root instead of the immediate parent folder. Pre-linked parent references on load and bound "Up" to `currentNode.parent` while ensuring breadcrumb ancestry paths remain 100% accurate at every depth.
- **Top Files Date Modified Column Clipping (GitHub #4)**:
  - Expanded the Date Modified column width to 180px with dedicated right-hand cell padding (`Margin="8,0,20,0"`), preventing text clipping from the DataGrid border and vertical scrollbar gutter. Added sorting by actual date (`LastModified`) instead of string text.

## [0.1.0-alpha] - 2026-10-05

### Added
- **True Single-File Executable Packaging**:
  - Configured `PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`, and `EnableCompressionInSingleFile` in project settings. Release builds now produce a clean, 100% standalone `StorageVisualiser.exe` with zero loose DLLs or framework runtime files in the publish output.
- **Custom High-Performance PercentageBar Control**:
  - Replaced Avalonia FluentTheme `ProgressBar` controls with a bespoke, lightweight `PercentageBar` control across Tree, Top Files, and File Types tabs.
  - Resolves FluentTheme minimum-width clipping, rounded line constraints, and proportional squaring issues that caused percentage fills to appear blank or disproportionately tiny when columns were narrow.
  - Expanded Tree percentage bar width from 100px to 135px for enhanced readability.
- **Drive Utilization & Tree Percentage Scaling**:
  - Aligned root drive used percentage ($28.1\%$) and free space ($71.9\%$) to sum to exactly $100.0\%$, eliminating discrepancy caused by unscanned locked OS metadata.
  - Corrected Tree `% of Total` mode to scale subfolders relative to total scanned files, while `% of Parent` scales relative to immediate parent folder.
- **Treemap Edge Snapping & Seamless Boundaries**:
  - Snapped squarified rows and tiles flush to container edges in `TreemapLayoutEngine`, eliminating floating-point rounding gaps and bottom-right container offsets when adjusting detail slider levels.
  - Grouped all sub-threshold items into `<Other>` without dropping remaining capacity.
- **`<Other>` Group Drill-Down & Context Menu Permissions**:
  - Double-clicking or right-clicking and selecting **"🔍 Drill Down into Group"** on any `<Other (N items)>` block now opens that batch as its own interactive treemap view, allowing full inspection of all grouped small files.
  - Preserved original parent node references for all grouped items so drilling down, navigating Up (`▲ Up`), or opening containing folders remains completely accurate and seamless.
  - Fixed context menu permissions for virtual aggregators: disabled "Open in Explorer", "Copy Full Path", "Delete", and "Properties" on `<Other>` groups, while keeping them fully enabled for real files and folders inside.
- **Navigation Shortcuts & Drag-and-Drop Scanning**:
  - Drag and drop any folder or drive from Windows Explorer straight into the window to trigger a scan.
  - Keyboard navigation shortcuts: `F5` to Rescan, `Backspace` / `Alt+Up` to navigate up, and `Alt+Left` / `Alt+Right` for Back / Forward history.
- **Assembly Metadata & Release Pipeline**:
  - Embedded release assembly metadata (Version `0.1.0-alpha`, Product, Authors, Copyright).
  - GitHub Actions automated release pipeline (`.github/workflows/release.yml`) producing single-file x64 binaries, release archives, and SHA-256 checksums on tag pushes.
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
