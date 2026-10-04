using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace StorageVisualiser.Windows.Shell;

public static class WindowsShellHelper
{
    public static void OpenInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Fallback: open parent or path directly
            OpenFileOrFolder(path);
        }
    }

    public static void OpenFileOrFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore failure to launch default handler
        }
    }

    public static bool ShowFileProperties(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var info = new SHELLEXECUTEINFO
        {
            cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
            lpVerb = "properties",
            lpFile = path,
            nShow = 5, // SW_SHOW
            fMask = 0x0000000C // SEE_MASK_INVOKEIDLIST
        };

        return ShellExecuteEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public nint hwnd;
        public string lpVerb;
        public string lpFile;
        public string lpParameters;
        public string lpDirectory;
        public int nShow;
        public nint hInstApp;
        public nint lpIDList;
        public string lpClass;
        public nint hkeyClass;
        public uint dwHotKey;
        public nint hIcon;
        public nint hProcess;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    private const int SW_SHOWNORMAL = 1;

    public static void EnsureWindowVisible(nint hwnd, int width, int height)
    {
        if (hwnd == 0) return;

        GetWindowRect(hwnd, out var rect);
        int currentWidth = rect.Right - rect.Left;
        int currentHeight = rect.Bottom - rect.Top;

        if (currentWidth <= 10 || currentHeight <= 10)
        {
            const uint SWP_SHOWWINDOW = 0x0040;
            const uint SWP_NOZORDER = 0x0004;
            SetWindowPos(hwnd, 0, 100, 100, width, height, SWP_SHOWWINDOW | SWP_NOZORDER);
        }

        ShowWindow(hwnd, SW_SHOWNORMAL);
        SetForegroundWindow(hwnd);
    }
}
