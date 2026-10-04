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
- **Unit Tests**:
  - Comprehensive unit test suite covering `StorageAnalysisEngine` top-files min-heap, extension aggregator, `FileActionService` protected path enforcement and logging, and `HtmlReportExporter` offline compliance and safe encoding (27 tests passing).
- **Standalone Portable Release**:
  - Zero-prerequisite single-file executable for Windows x64 (`StorageVisualiser-win-x64.zip`).

### Fixed
- **Window Sizing & Spacious Default Layout**: Increased default window dimensions to 1260x800 (minimum 960x540) to prevent toolbar controls from crowding or clipping on high-DPI laptop displays and standard desktop monitors.
- **Streamlined Toolbar**: Consolidated redundant rescan buttons into a single smart `Rescan` command, polished `Export Report...` button typography, and reduced ComboBox width to guarantee ample breathing room across all screen resolutions.
- **Top Files DataGrid Headers**: Expanded the `Extension` column width to 115px to prevent header truncation (`Extensic`) and balanced `Size` (110px) and `Date Modified` (150px).
- **HTML Export Canvas Crispness**: Eliminated text blurriness by rendering on exact integer pixel coordinates, implementing high-DPI canvas scaling (`ctx.scale(dpr, dpr)`), and avoiding canvas `maxWidth` font squashing.
- **Recursive Bisection Treemap for HTML**: Upgraded HTML canvas treemap layout to recursive bisection, preventing razor-thin slats and bottom clipping.
- **Interactive Clickable Breadcrumbs**: Transformed HTML export breadcrumb path into clean, clickable navigation segments (`Home › Drive › Subfolder`) and fixed duplicate root breadcrumbs upon header double-click.
- **Clutter Elimination in Reports**: Fixed exporter `<Other>` grouping threshold to group all sub-threshold items without forcing the first 20 tiny items into microscopic slivers.
