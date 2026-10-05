using System;
using System.Threading;
using System.Threading.Tasks;
using StorageVisualiser.Core.Model;
using StorageVisualiser.Core.Scanning;

namespace StorageVisualiser.Windows.Scanning;

public sealed class WindowsAutoScanner : IScanner
{
    private readonly WindowsNtfsMftScanner _mftScanner = new();
    private readonly DirectoryWalkerScanner _walkerScanner = new();

    public static Action<string>? LogAction { get; set; }

    public string ScannerName => "Auto (MFT / Directory Walker)";

    public string ActiveScannerName { get; private set; } = "Directory Walker";

    public async Task<StorageNode> ScanAsync(
        ScanTarget target,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        bool canMft = WindowsNtfsMftScanner.CanScan(target);
        LogAction?.Invoke($"[WindowsAutoScanner] Target='{target.RootPath}', IsDriveRoot={target.IsDriveRoot}, IsAdmin={WindowsNtfsMftScanner.IsAdministrator()}, CanScan={canMft}");

        if (canMft)
        {
            try
            {
                ActiveScannerName = _mftScanner.ScannerName;
                return await _mftScanner.ScanAsync(target, progress, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // MFT scan failed - log full exception and fallback to Directory Walker
                LogAction?.Invoke($"[WindowsAutoScanner] MFT scan failed for '{target.RootPath}': {ex}. Falling back to Directory Walker.");
                System.Diagnostics.Debug.WriteLine($"[WindowsAutoScanner] MFT scan failed for '{target.RootPath}': {ex.Message}. Falling back to Directory Walker.");
            }
        }

        ActiveScannerName = _walkerScanner.ScannerName;
        return await _walkerScanner.ScanAsync(target, progress, cancellationToken).ConfigureAwait(false);
    }
}
