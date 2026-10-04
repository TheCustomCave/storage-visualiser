using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using StorageVisualiser.Core.Actions;

namespace StorageVisualiser.Windows.Shell;

public sealed class WindowsRecycleBinProvider : IRecycleBinProvider
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public nint hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public nint hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHFileOperation([In] ref SHFILEOPSTRUCT lpFileOp);

    public bool SendToRecycleBin(string path, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "Path cannot be empty.";
            return false;
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            errorMessage = "Item does not exist on disk.";
            return false;
        }

        try
        {
            // SHFileOperation requires double null termination for pFrom
            var doubleNullTerminatedPath = path + "\0\0";

            var fileOp = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = doubleNullTerminatedPath,
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
            };

            int result = SHFileOperation(ref fileOp);
            if (result != 0)
            {
                var win32Ex = new Win32Exception(result);
                errorMessage = $"Shell operation failed with code {result}: {win32Ex.Message}";
                return false;
            }

            if (fileOp.fAnyOperationsAborted)
            {
                errorMessage = "Operation was aborted by the user or system.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
