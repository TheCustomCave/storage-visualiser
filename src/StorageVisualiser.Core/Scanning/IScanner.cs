using System;
using System.Threading;
using System.Threading.Tasks;
using StorageVisualiser.Core.Model;

namespace StorageVisualiser.Core.Scanning;

public interface IScanner
{
    string ScannerName { get; }
    Task<StorageNode> ScanAsync(
        ScanTarget target,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken);
}
