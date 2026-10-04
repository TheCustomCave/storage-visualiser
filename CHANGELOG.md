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
- **Unit Tests**:
  - Comprehensive unit test suite for `StorageAnalysisEngine` top-files min-heap, extension aggregator, and display formatting.
- **Standalone Portable Release**:
  - Zero-prerequisite single-file executable for Windows x64 (`StorageVisualiser-win-x64.zip`).
