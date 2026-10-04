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

    public string ScannerName => "Auto (MFT / Directory Walker)";

    public string ActiveScannerName { get; private set; } = "Directory Walker";

    public async Task<StorageNode> ScanAsync(
        ScanTarget target,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (WindowsNtfsMftScanner.CanScan(target))
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
                // MFT scan failed (e.g. security permission, exclusive lock or geometry issue) - fallback to Directory Walker
                System.Diagnostics.Debug.WriteLine($"[WindowsAutoScanner] MFT scan failed for '{target.RootPath}': {ex.Message}. Falling back to Directory Walker.");
            }
        }

        ActiveScannerName = _walkerScanner.ScannerName;
        return await _walkerScanner.ScanAsync(target, progress, cancellationToken).ConfigureAwait(false);
    }
}
