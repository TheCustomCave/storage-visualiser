using Avalonia;
using System;

namespace StorageVisualiser.App;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    public static void Log(string msg)
    {
        try { System.IO.File.AppendAllText("startup.log", $"[{DateTime.Now:HH:mm:ss.fff}] [PID:{Environment.ProcessId}] {msg}\r\n"); } catch {}
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public nint lpReserved2;
        public nint hStdInput;
        public nint hStdOutput;
        public nint hStdError;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public nint hProcess;
        public nint hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        string lpCommandLine,
        nint lpProcessAttributes,
        nint lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        nint lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool GetExitCodeProcess(nint hProcess, out uint lpExitCode);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint hObject);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern int GetCurrentThreadId();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetThreadDesktop(int dwThreadId);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetUserObjectInformation(IntPtr hObj, int nIndex, [System.Runtime.InteropServices.Out] byte[] pvInfo, int nLength, out int lpnLengthNeeded);

    private static void EnsureRunningOnDefaultDesktop(string[] args)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            IntPtr cur = GetThreadDesktop(GetCurrentThreadId());
            byte[] bytes = new byte[256];
            GetUserObjectInformation(cur, 2, bytes, 256, out int len);
            string curName = System.Text.Encoding.ASCII.GetString(bytes, 0, len).TrimEnd('\0');
            Log($"Current thread desktop: {curName}");

            bool isOnDesktopArg = Array.Exists(args, a => string.Equals(a, "--on-desktop", StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(curName, "default", StringComparison.OrdinalIgnoreCase) && !isOnDesktopArg)
            {
                Log($"Non-default desktop '{curName}' detected. Relaying process to WinSta0\\default...");
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var si = new STARTUPINFO();
                    si.cb = System.Runtime.InteropServices.Marshal.SizeOf(si);
                    si.lpDesktop = @"WinSta0\default";

                    var cmdLine = $"\"{exePath}\" {string.Join(" ", args)} --on-desktop";
                    if (CreateProcess(null, cmdLine, IntPtr.Zero, IntPtr.Zero, true, 0, IntPtr.Zero, null, ref si, out var pi))
                    {
                        Log($"Child process spawned on WinSta0\\default (PID: {pi.dwProcessId}). Waiting for exit...");
                        CloseHandle(pi.hThread);
                        _ = WaitForSingleObject(pi.hProcess, 0xFFFFFFFF);
                        _ = GetExitCodeProcess(pi.hProcess, out uint exitCode);
                        CloseHandle(pi.hProcess);
                        Log($"Child process exited with code: {exitCode}");
                        Environment.Exit((int)exitCode);
                    }
                    else
                    {
                        Log($"CreateProcess on WinSta0\\default failed: error={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"EnsureRunningOnDefaultDesktop exception: {ex}");
        }
    }

    [STAThread]
    public static void Main(string[] args)
    {
        EnsureRunningOnDefaultDesktop(args);
        Log("Program.Main entered");
        StorageVisualiser.Windows.Scanning.WindowsAutoScanner.LogAction = Log;
        StorageVisualiser.Windows.Scanning.WindowsNtfsMftScanner.LogAction = Log;
        try
        {
            Log("Calling StartWithClassicDesktopLifetime...");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            Log("StartWithClassicDesktopLifetime returned cleanly");
        }
        catch (Exception ex)
        {
            Log("FATAL EXCEPTION: " + ex);
            Console.Error.WriteLine("[StorageVisualiser] Exception: " + ex.Message);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
