using System;
using System.IO;
using System.Text.RegularExpressions;
using Shouldly;
using StorageVisualiser.Core.Export;
using StorageVisualiser.Core.Model;
using Xunit;

namespace StorageVisualiser.Core.Tests;

public class HtmlReportExporterTests
{
    [Fact]
    public void GenerateDefaultFileName_CreatesValidWindowsFileName()
    {
        var fileName = HtmlReportExporter.GenerateDefaultFileName(@"C:\Projects\My App", "html");

        fileName.ShouldEndWith(".html");
        fileName.ShouldStartWith("Storage Report - ");
        fileName.ShouldNotContain(":");
        fileName.ShouldNotContain("/");
        fileName.ShouldNotContain("\\");
        Path.GetInvalidFileNameChars().Any(c => fileName.Contains(c)).ShouldBeFalse();
    }

    [Fact]
    public void ExportToHtml_GeneratesValidOfflineHtmlWithNoNetworkRequests()
    {
        var root = new StorageNode { Name = @"C:\Scan", Kind = StorageItemKind.Directory, Size = 1_000_000, FileCount = 3, DirectoryCount = 1 };
        var subDir = new StorageNode { Name = "Documents", Kind = StorageItemKind.Directory, Size = 600_000, FileCount = 2, DirectoryCount = 0 };
        root.AddChild(subDir);

        var file1 = new StorageNode { Name = "data.csv", Kind = StorageItemKind.File, Size = 400_000, FileCount = 1 };
        var file2 = new StorageNode { Name = "report.docx", Kind = StorageItemKind.File, Size = 300_000, FileCount = 1 };
        var file3 = new StorageNode { Name = "notes.txt", Kind = StorageItemKind.File, Size = 300_000, FileCount = 1 };

        root.AddChild(file1);
        subDir.AddChild(file2);
        subDir.AddChild(file3);

        var html = HtmlReportExporter.ExportToHtml(root, @"C:\Scan", scanDuration: "00:02", maxTreeDepth: 5);

        // Standard HTML structure
        html.ShouldStartWith("<!DOCTYPE html>");
        html.ShouldContain("<html lang=\"en\">");
        html.ShouldContain("</html>");
        html.ShouldContain("<canvas id=\"treemap-canvas\"");
        html.ShouldContain("reportData");
        html.ShouldContain("data.csv");
        html.ShouldContain("report.docx");

        // Strict Offline Compliance (AGENTS.md): No remote URLs or CDN scripts/fonts
        html.ShouldNotContain("http://");
        html.ShouldNotContain("https://");
        html.ShouldNotContain("fonts.googleapis.com");
        html.ShouldNotContain("cdnjs.cloudflare.com");
    }

    [Fact]
    public void ExportToHtml_HandlesSpecialCharactersSafely()
    {
        var root = new StorageNode { Name = "Test<>&\"'Path", Kind = StorageItemKind.Directory, Size = 500, FileCount = 1 };
        var specialFile = new StorageNode { Name = "file<script>alert(1)</script>&name.txt", Kind = StorageItemKind.File, Size = 500, FileCount = 1 };
        root.AddChild(specialFile);

        var html = HtmlReportExporter.ExportToHtml(root, "Test<>&\"'Path", scanDuration: "00:01");

        // The HTML itself must not execute untrusted script tags injected into titles/headers
        html.ShouldNotContain("<script>alert(1)</script>");
        // Header title is HTML encoded
        html.ShouldContain("Test&lt;&gt;&amp;&quot;&#39;Path");
        // System.Text.Json default encoder safely escapes < and > and & in json strings
        html.ShouldContain(@"file\u003Cscript\u003Ealert(1)\u003C/script\u003E");
    }

    [Fact]
    public void ExportToHtml_IncludesParentLinkingAndUpNavigationScript()
    {
        var root = new StorageNode { Name = "C:\\", Kind = StorageItemKind.Directory, Size = 1_000 };
        var html = HtmlReportExporter.ExportToHtml(root, "C:\\", scanDuration: "00:01");

        // Verify parent linking function is present
        html.ShouldContain("function linkParents(node, parent)");
        html.ShouldContain("linkParents(reportData.treeRoot, null);");
        // Verify Up navigation uses parent reference rather than popping history directly to root
        html.ShouldContain("currentNode.parent");
        html.ShouldContain("function navigateUp()");
    }
}

