using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using StorageVisualiser.Core.Analysis;
using StorageVisualiser.Core.Formatting;
using StorageVisualiser.Core.Model;

namespace StorageVisualiser.Core.Export;

public static class HtmlReportExporter
{
    public static string GenerateDefaultFileName(string targetPath, string extension = "html")
    {
        var host = Environment.MachineName;
        var cleanTarget = targetPath.TrimEnd('\\', '/').Replace(":", "", StringComparison.Ordinal).Replace('\\', '_').Replace('/', '_');
        if (string.IsNullOrWhiteSpace(cleanTarget)) cleanTarget = "Root";

        // ISO 8601 basic time format without colons for Windows filename compatibility
        var timestamp = DateTime.Now.ToString("yyyy-MM-ddTHHmmss", CultureInfo.InvariantCulture);
        return $"Storage Report - {host} - {cleanTarget} - {timestamp}.{extension}";
    }

    public static string ExportToHtml(
        StorageNode root,
        string targetPath,
        string scanDuration = "",
        int maxTreeDepth = 5)
    {
        ArgumentNullException.ThrowIfNull(root);

        var topFiles = StorageAnalysisEngine.GetTopFiles(root, 200).Select(f => new ExportTopFileDto
        {
            Name = f.Name,
            FullPath = f.FullPath,
            Extension = f.Extension,
            Size = f.Size,
            FormattedSize = f.FormattedSize,
            Percentage = f.PercentageOfTotal,
            FormattedPercentage = f.FormattedPercentage,
            Modified = f.FormattedModified
        }).ToList();

        var fileTypes = StorageAnalysisEngine.GetFileTypeBreakdown(root).Select(t => new ExportFileTypeDto
        {
            Extension = t.Extension,
            Category = t.Category,
            TotalSize = t.TotalSize,
            FormattedTotalSize = t.FormattedTotalSize,
            Percentage = t.PercentageOfTotal,
            FormattedPercentage = t.FormattedPercentage,
            FileCount = t.FileCount,
            FormattedFileCount = t.FormattedFileCount
        }).ToList();

        var metadata = new ReportMetadata
        {
            HostName = Environment.MachineName,
            TargetPath = string.IsNullOrWhiteSpace(targetPath) ? root.GetFullPath() : targetPath,
            GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            TotalSizeBytes = root.Size,
            FormattedTotalSize = SizeFormatter.Format(root.Size),
            FileCount = root.FileCount,
            DirectoryCount = root.DirectoryCount,
            ScanDuration = scanDuration
        };

        var treeDto = BuildExportTree(root, maxTreeDepth, 0);

        var fullData = new FullReportData
        {
            Metadata = metadata,
            TreeRoot = treeDto,
            TopFiles = topFiles,
            FileTypes = fileTypes
        };

        var jsonString = JsonSerializer.Serialize(fullData, ReportJsonContext.Default.FullReportData);

        return BuildHtmlDocument(metadata, jsonString);
    }

    private static ExportNodeDto BuildExportTree(StorageNode node, int maxDepth, int currentDepth)
    {
        var dto = new ExportNodeDto
        {
            Name = node.Name,
            Size = node.Size,
            Kind = (byte)node.Kind
        };

        if (currentDepth < maxDepth && node.HasChildren)
        {
            dto.Children = new List<ExportNodeDto>();
            // Sort by size descending
            var sorted = node.Children.OrderByDescending(c => c.Size);
            long otherSize = 0;
            int otherCount = 0;
            long minThreshold = (long)(node.Size * 0.005); // 0.5% threshold for tree

            foreach (var child in sorted)
            {
                if (child.Kind == StorageItemKind.DriveFreeSpace)
                {
                    dto.Children.Add(new ExportNodeDto
                    {
                        Name = child.Name,
                        Size = child.Size,
                        Kind = (byte)child.Kind
                    });
                    continue;
                }

                if (child.Size >= minThreshold || dto.Children.Count < 20)
                {
                    dto.Children.Add(BuildExportTree(child, maxDepth, currentDepth + 1));
                }
                else
                {
                    otherSize += child.Size;
                    otherCount++;
                }
            }

            if (otherCount > 0)
            {
                dto.Children.Add(new ExportNodeDto
                {
                    Name = $"<Other ({otherCount:N0} items)>",
                    Size = otherSize,
                    Kind = 4 // OtherGroup
                });
            }
        }

        return dto;
    }

    private static string BuildHtmlDocument(ReportMetadata meta, string jsonPayload)
    {
        var sb = new StringBuilder();
        sb.Append("""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Storage Report — 
""");
        sb.Append(System.Net.WebUtility.HtmlEncode(meta.TargetPath));
        sb.Append("""

</title>
<style>
:root {
  --bg: #F8FAFC;
  --surface: #FFFFFF;
  --border: #E2E8F0;
  --text: #0F172A;
  --text-muted: #64748B;
  --accent: #2563EB;
  --accent-light: #93C5FD;
}
* { box-sizing: border-box; margin: 0; padding: 0; }
body {
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
  background: var(--bg);
  color: var(--text);
  line-height: 1.5;
  padding: 16px 24px;
}
.header {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 16px 20px;
  margin-bottom: 16px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
}
.header-title h1 {
  font-size: 1.25rem;
  font-weight: 700;
  display: flex;
  align-items: center;
  gap: 8px;
}
.header-meta {
  font-size: 0.825rem;
  color: var(--text-muted);
}
.header-stats {
  display: flex;
  gap: 16px;
}
.stat-pill {
  background: #F1F5F9;
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 6px 14px;
  text-align: center;
}
.stat-pill .val { font-size: 1.1rem; font-weight: 700; color: var(--accent); }
.stat-pill .lbl { font-size: 0.72rem; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.5px; }

.privacy-banner {
  background: #FFFBEB;
  border: 1px solid #FDE68A;
  border-radius: 6px;
  padding: 8px 14px;
  font-size: 0.8rem;
  color: #92400E;
  margin-bottom: 16px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.tabs {
  display: flex;
  gap: 4px;
  border-bottom: 2px solid var(--border);
  margin-bottom: 16px;
}
.tab-btn {
  background: transparent;
  border: none;
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--text-muted);
  padding: 10px 18px;
  cursor: pointer;
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
  transition: all 0.15s ease;
}
.tab-btn.active {
  color: var(--accent);
  border-bottom-color: var(--accent);
}
.tab-btn:hover { color: var(--text); }

.tab-pane { display: none; }
.tab-pane.active { display: block; }

.nav-bar {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 8px 12px;
  margin-bottom: 10px;
  display: flex;
  align-items: center;
  gap: 8px;
}
.btn {
  background: #F1F5F9;
  border: 1px solid var(--border);
  border-radius: 5px;
  padding: 5px 12px;
  font-size: 0.8rem;
  font-weight: 600;
  cursor: pointer;
}
.btn:hover { background: #E2E8F0; }
.breadcrumbs {
  font-family: Consolas, monospace;
  font-size: 0.825rem;
  color: #334155;
  background: #F8FAFC;
  border: 1px solid var(--border);
  border-radius: 4px;
  padding: 5px 10px;
  flex: 1;
}

#treemap-canvas {
  width: 100%;
  height: 620px;
  background: #FFFFFF;
  border: 1px solid var(--border);
  border-radius: 6px;
  display: block;
}

.status-bar {
  background: #F1F5F9;
  border: 1px solid var(--border);
  border-radius: 5px;
  padding: 8px 14px;
  margin-top: 8px;
  font-size: 0.8rem;
  font-weight: 600;
  color: #1E40AF;
  min-height: 34px;
}

.table-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
  overflow: hidden;
}
.table-header {
  padding: 12px 16px;
  background: #F8FAFC;
  border-bottom: 1px solid var(--border);
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.search-input {
  border: 1px solid var(--border);
  border-radius: 5px;
  padding: 6px 12px;
  font-size: 0.825rem;
  width: 260px;
}
table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.825rem;
}
th {
  background: #F1F5F9;
  text-align: left;
  padding: 10px 14px;
  font-weight: 600;
  color: var(--text-muted);
  border-bottom: 1px solid var(--border);
}
td {
  padding: 8px 14px;
  border-bottom: 1px solid #F1F5F9;
}
tr:hover td { background: #F8FAFC; }
.progress-bar-wrap {
  background: #F1F5F9;
  border: 1px solid #CBD5E1;
  border-radius: 3px;
  height: 15px;
  position: relative;
  overflow: hidden;
  width: 110px;
}
.progress-fill {
  height: 100%;
  background: #93C5FD;
}
.progress-fill.red { background: #FCA5A5; }
.progress-fill.green { background: #86EFAC; }
.progress-text {
  position: absolute;
  top: 0; left: 0; right: 0; bottom: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: 700;
  color: #0F172A;
}
</style>
</head>
<body>

<div class="header">
  <div class="header-title">
    <h1>📦 Storage Visualiser — Storage Report</h1>
    <div class="header-meta">Target: <strong>
""");
        sb.Append(System.Net.WebUtility.HtmlEncode(meta.TargetPath));
        sb.Append("""
</strong> | Host: <strong>
""");
        sb.Append(System.Net.WebUtility.HtmlEncode(meta.HostName));
        sb.Append("""
</strong> | Date: 
""");
        sb.Append(System.Net.WebUtility.HtmlEncode(meta.GeneratedAt));
        if (!string.IsNullOrEmpty(meta.ScanDuration))
        {
            sb.Append(" | Duration: ");
            sb.Append(System.Net.WebUtility.HtmlEncode(meta.ScanDuration));
        }
        sb.Append("""

</div>
  </div>
  <div class="header-stats">
    <div class="stat-pill"><div class="val">
""");
        sb.Append(System.Net.WebUtility.HtmlEncode(meta.FormattedTotalSize));
        sb.Append("""

</div><div class="lbl">Total Size</div></div>
    <div class="stat-pill"><div class="val">
""");
        sb.Append(meta.FileCount.ToString("N0", CultureInfo.InvariantCulture));
        sb.Append("""

</div><div class="lbl">Files</div></div>
    <div class="stat-pill"><div class="val">
""");
        sb.Append(meta.DirectoryCount.ToString("N0", CultureInfo.InvariantCulture));
        sb.Append("""

</div><div class="lbl">Folders</div></div>
  </div>
</div>

<div class="privacy-banner">
  <span>ℹ️</span>
  <span><strong>Privacy notice:</strong> This report contains file and folder names scanned from the target system. Designed for offline review.</span>
</div>

<div class="tabs">
  <button class="tab-btn active" onclick="switchTab('map')">🗺️ Map</button>
  <button class="tab-btn" onclick="switchTab('top')">📄 Top Files (Largest 200)</button>
  <button class="tab-btn" onclick="switchTab('types')">📊 File Types</button>
</div>

<!-- Map Tab -->
<div id="pane-map" class="tab-pane active">
  <div class="nav-bar">
    <button class="btn" onclick="navigateUp()">⬆️ Up</button>
    <button class="btn" onclick="navigateHome()">🏠 Home</button>
    <div id="breadcrumbs" class="breadcrumbs"></div>
  </div>
  <canvas id="treemap-canvas"></canvas>
  <div id="status-bar" class="status-bar">Click a folder to inspect, or double-click to drill down.</div>
</div>

<!-- Top Files Tab -->
<div id="pane-top" class="tab-pane">
  <div class="table-card">
    <div class="table-header">
      <strong>Largest 200 Files</strong>
      <input type="text" id="top-search" class="search-input" placeholder="Filter by name, path or ext..." oninput="filterTopFiles()">
    </div>
    <table>
      <thead>
        <tr>
          <th>File Name</th>
          <th>Size</th>
          <th>% of Total</th>
          <th>Extension</th>
          <th>Path</th>
          <th>Modified</th>
        </tr>
      </thead>
      <tbody id="top-files-tbody"></tbody>
    </table>
  </div>
</div>

<!-- File Types Tab -->
<div id="pane-types" class="tab-pane">
  <div class="table-card">
    <div class="table-header">
      <strong>File Extension &amp; Category Breakdown</strong>
    </div>
    <table>
      <thead>
        <tr>
          <th>Extension</th>
          <th>Category</th>
          <th>Total Size</th>
          <th>% of Total</th>
          <th>File Count</th>
        </tr>
      </thead>
      <tbody id="file-types-tbody"></tbody>
    </table>
  </div>
</div>

<script>
const reportData = 
""");
        sb.Append(jsonPayload);
        sb.Append("""

;

// State
let currentNode = reportData.treeRoot;
const navHistory = [];
let hoveredBox = null;
let currentBoxes = [];

const palette = [
  { c: "#FFEBE8", h: "#FF7060" },
  { c: "#FFFDE7", h: "#FFE040" },
  { c: "#E8F5E9", h: "#76D275" },
  { c: "#E1F5FE", h: "#40C4FF" },
  { c: "#F3E5F5", h: "#BA68C8" },
  { c: "#FBE9E7", h: "#FF8A65" },
  { c: "#E0F2F1", h: "#4DB6AC" }
];
const fileColor = "#FFCCBC";
const freeSpaceColor = "#F1F5F9";

function formatBytes(bytes) {
  if (bytes === 0) return '0 bytes';
  const k = 1024;
  const sizes = ['bytes', 'KB', 'MB', 'GB', 'TB', 'PB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  const val = bytes / Math.pow(k, i);
  return (val >= 100 ? val.toFixed(0) : val >= 10 ? val.toFixed(1) : val.toFixed(2)) + ' ' + sizes[i];
}

function switchTab(tabId) {
  document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
  document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));
  if (tabId === 'map') {
    document.querySelectorAll('.tab-btn')[0].classList.add('active');
    document.getElementById('pane-map').classList.add('active');
    renderTreemap();
  } else if (tabId === 'top') {
    document.querySelectorAll('.tab-btn')[1].classList.add('active');
    document.getElementById('pane-top').classList.add('active');
  } else {
    document.querySelectorAll('.tab-btn')[2].classList.add('active');
    document.getElementById('pane-types').classList.add('active');
  }
}

function renderTreemap() {
  const canvas = document.getElementById('treemap-canvas');
  const dpr = window.devicePixelRatio || 1;
  const rect = canvas.getBoundingClientRect();
  canvas.width = rect.width * dpr;
  canvas.height = rect.height * dpr;
  const ctx = canvas.getContext('2d');
  ctx.scale(dpr, dpr);

  currentBoxes = [];
  document.getElementById('breadcrumbs').innerText = getBreadcrumbsText();

  layoutNode(ctx, currentNode, 0, 0, rect.width, rect.height, 0);
}

function getBreadcrumbsText() {
  const parts = navHistory.map(n => n.n);
  parts.push(currentNode.n);
  return parts.join(' \\ ');
}

function navigateUp() {
  if (navHistory.length > 0) {
    currentNode = navHistory.pop();
    renderTreemap();
  }
}

function navigateHome() {
  if (navHistory.length > 0) {
    navHistory.length = 0;
    currentNode = reportData.treeRoot;
    renderTreemap();
  }
}

function layoutNode(ctx, node, x, y, w, h, depth) {
  if (w <= 2 || h <= 2) return;

  const isDir = node.k === 1;
  const isFree = node.k === 2;
  const isOther = node.k === 4;

  if (!isDir || !node.c || node.c.length === 0 || depth >= 5) {
    // Leaf item
    currentBoxes.push({ node, x, y, w, h, depth });
    ctx.lineWidth = 1;
    ctx.strokeStyle = '#222222';

    if (isFree) {
      ctx.fillStyle = freeSpaceColor;
    } else if (isOther) {
      ctx.fillStyle = '#E2E8F0';
    } else if (isDir) {
      ctx.fillStyle = palette[depth % palette.length].c;
    } else {
      ctx.fillStyle = fileColor;
    }

    ctx.fillRect(x, y, w, h);
    ctx.strokeRect(x, y, w, h);

    if (w >= 30 && h >= 14) {
      ctx.fillStyle = '#111111';
      ctx.font = '10px -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
      ctx.fillText(node.n, x + 3, y + 11, w - 6);
    }
    return;
  }

  // Directory container with header
  currentBoxes.push({ node, x, y, w, h, depth });
  const theme = palette[depth % palette.length];
  const headerHeight = Math.min(18, Math.max(12, h * 0.15));

  // Draw Header
  ctx.fillStyle = theme.h;
  ctx.fillRect(x, y, w, headerHeight);
  ctx.strokeStyle = '#222222';
  ctx.strokeRect(x, y, w, headerHeight);

  if (w >= 20 && headerHeight >= 10) {
    ctx.fillStyle = '#111111';
    ctx.font = 'bold 11px -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
    ctx.fillText(`${node.n} (${formatBytes(node.s)})`, x + 4, y + headerHeight - 4, w - 8);
  }

  // Children area
  const cx = x + 1;
  const cy = y + headerHeight + 1;
  const cw = w - 2;
  const ch = h - headerHeight - 2;

  if (cw <= 4 || ch <= 4) return;

  // Squarified layout subdivision
  layoutChildren(ctx, node.c, cx, cy, cw, ch, depth + 1);
}

function layoutChildren(ctx, children, x, y, w, h, depth) {
  const total = children.reduce((acc, c) => acc + Math.max(0, c.s), 0);
  if (total <= 0) return;

  let remainingX = x;
  let remainingY = y;
  let remainingW = w;
  let remainingH = h;
  let remainingTotal = total;

  for (let i = 0; i < children.length; i++) {
    const child = children[i];
    const fraction = Math.max(0, child.s) / remainingTotal;
    const isHorizontal = remainingW >= remainingH;

    if (i === children.length - 1) {
      layoutNode(ctx, child, remainingX, remainingY, remainingW, remainingH, depth);
      break;
    }

    if (isHorizontal) {
      const itemW = Math.max(1, remainingW * fraction);
      layoutNode(ctx, child, remainingX, remainingY, itemW, remainingH, depth);
      remainingX += itemW;
      remainingW = Math.max(0, remainingW - itemW);
    } else {
      const itemH = Math.max(1, remainingH * fraction);
      layoutNode(ctx, child, remainingX, remainingY, remainingW, itemH, depth);
      remainingY += itemH;
      remainingH = Math.max(0, remainingH - itemH);
    }

    remainingTotal = Math.max(1, remainingTotal - child.s);
    if (remainingW <= 2 || remainingH <= 2) break;
  }
}

// Interaction
const canvas = document.getElementById('treemap-canvas');
canvas.addEventListener('mousemove', e => {
  const rect = canvas.getBoundingClientRect();
  const mx = e.clientX - rect.left;
  const my = e.clientY - rect.top;

  let hit = null;
  for (let i = currentBoxes.length - 1; i >= 0; i--) {
    const b = currentBoxes[i];
    if (mx >= b.x && mx <= b.x + b.w && my >= b.y && my <= b.y + b.h) {
      hit = b;
      break;
    }
  }

  if (hit) {
    const n = hit.node;
    document.getElementById('status-bar').innerText = `${n.n} | ${formatBytes(n.s)}`;
  }
});

canvas.addEventListener('dblclick', e => {
  const rect = canvas.getBoundingClientRect();
  const mx = e.clientX - rect.left;
  const my = e.clientY - rect.top;

  for (let i = currentBoxes.length - 1; i >= 0; i--) {
    const b = currentBoxes[i];
    if (mx >= b.x && mx <= b.x + b.w && my >= b.y && my <= b.y + b.h) {
      if (b.node.k === 1 && b.node.c && b.node.c.length > 0) {
        navHistory.push(currentNode);
        currentNode = b.node;
        renderTreemap();
      }
      break;
    }
  }
});

// Populate Top Files Table
function populateTopFiles(files) {
  const tbody = document.getElementById('top-files-tbody');
  tbody.innerHTML = '';
  files.forEach(f => {
    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td><strong>${escapeHtml(f.name)}</strong></td>
      <td><code>${f.formattedSize}</code></td>
      <td>
        <div class="progress-bar-wrap">
          <div class="progress-fill red" style="width: ${Math.min(100, f.percentage)}%"></div>
          <div class="progress-text">${f.formattedPercentage}</div>
        </div>
      </td>
      <td>${escapeHtml(f.extension)}</td>
      <td style="color: #64748B">${escapeHtml(f.fullPath)}</td>
      <td style="color: #64748B">${f.modified}</td>
    `;
    tbody.appendChild(tr);
  });
}

function filterTopFiles() {
  const q = document.getElementById('top-search').value.toLowerCase();
  const filtered = reportData.topFiles.filter(f =>
    f.name.toLowerCase().includes(q) ||
    f.fullPath.toLowerCase().includes(q) ||
    f.extension.toLowerCase().includes(q)
  );
  populateTopFiles(filtered);
}

// Populate File Types Table
function populateFileTypes() {
  const tbody = document.getElementById('file-types-tbody');
  tbody.innerHTML = '';
  reportData.fileTypes.forEach(t => {
    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td><strong>${escapeHtml(t.extension)}</strong></td>
      <td>${escapeHtml(t.category)}</td>
      <td><code>${t.formattedTotalSize}</code></td>
      <td>
        <div class="progress-bar-wrap">
          <div class="progress-fill green" style="width: ${Math.min(100, t.percentage)}%"></div>
          <div class="progress-text">${t.formattedPercentage}</div>
        </div>
      </td>
      <td>${t.formattedFileCount}</td>
    `;
    tbody.appendChild(tr);
  });
}

function escapeHtml(str) {
  if (!str) return '';
  return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#039;");
}

// Init
window.addEventListener('resize', renderTreemap);
populateTopFiles(reportData.topFiles);
populateFileTypes();
renderTreemap();
</script>
</body>
</html>
""");

        return sb.ToString();
    }
}
