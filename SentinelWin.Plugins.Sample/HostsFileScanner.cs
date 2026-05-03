using SentinelWin.Core.Abstractions;
using SentinelWin.Core.Models;

namespace SentinelWin.Plugins.Sample;

/// <summary>
/// Plugin de exemplo: detecta entradas no hosts file que apontam para localhost.
/// </summary>
public sealed class HostsFileScanner : IScanner
{
    public string Name => "Hosts File Scanner";

    public async Task<IReadOnlyList<ScanItem>> ScanAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                                "drivers", "etc", "hosts");
        if (!File.Exists(path))
            return Array.Empty<ScanItem>();

        var items = new List<ScanItem>();
        var lines = await File.ReadAllLinesAsync(path, ct);
        int idx = 0;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (line.StartsWith("127.0.0.1") || line.StartsWith("0.0.0.0"))
            {
                items.Add(new ScanItem(
                    Id: $"hosts:{idx++}",
                    Name: line,
                    Type: "Network",
                    Description: "Entrada de bloqueio no arquivo hosts.",
                    Status: "Active",
                    Risk: Risk.Low,
                    Recommendation: "Revisar se ainda é necessário"
                ));
            }
        }
        return items;
    }
}
