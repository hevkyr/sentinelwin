using System.Text.Json;
using SentinelWin.Core.Models;

namespace SentinelWin.Core.Services;

/// <summary>
/// Persists Snapshot records as JSON files.
/// Supports a custom storage directory for testability.
/// </summary>
public sealed class SnapshotStore
{
    private readonly string _root;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>Creates a store in %LOCALAPPDATA%\SentinelWin\snapshots.</summary>
    public SnapshotStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SentinelWin", "snapshots"))
    { }

    /// <summary>Creates a store in the specified directory (useful for unit tests).</summary>
    public SnapshotStore(string directory)
    {
        _root = directory;
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(Snapshot snap, CancellationToken ct = default)
    {
        var file = Path.Combine(_root, $"{snap.Id}.json");
        await using var fs = File.Create(file);
        await JsonSerializer.SerializeAsync(fs, snap, JsonOpts, ct);
    }

    public IEnumerable<string> List() =>
        Directory.EnumerateFiles(_root, "*.json")
                 .Select(Path.GetFileNameWithoutExtension)!;

    public async Task<Snapshot?> LoadAsync(string id, CancellationToken ct = default)
    {
        var file = Path.Combine(_root, $"{id}.json");
        if (!File.Exists(file)) return null;
        await using var fs = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<Snapshot>(fs, JsonOpts, ct);
    }

    public bool Delete(string id)
    {
        var file = Path.Combine(_root, $"{id}.json");
        if (!File.Exists(file)) return false;
        File.Delete(file);
        return true;
    }
}
