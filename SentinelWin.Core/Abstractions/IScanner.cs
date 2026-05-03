using SentinelWin.Core.Models;

namespace SentinelWin.Core.Abstractions;

public interface IScanner
{
    string Name { get; }
    Task<IReadOnlyList<ScanItem>> ScanAsync(CancellationToken ct = default);
}
