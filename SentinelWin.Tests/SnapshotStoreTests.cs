using SentinelWin.Core.Models;
using SentinelWin.Core.Services;
using Xunit;

namespace SentinelWin.Tests;

public class SnapshotStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SnapshotStore _store;

    public SnapshotStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _store = new SnapshotStore(_tempDir);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsCorrectly()
    {
        var snap = new Snapshot(
            Id: "abc123",
            CreatedAt: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            ItemId: "svc:DiagTrack",
            Type: "Service",
            PreviousState: "Running/Automatic",
            NewState: "Stopped/Disabled",
            ServiceName: "DiagTrack"
        );

        await _store.SaveAsync(snap);
        var loaded = await _store.LoadAsync("abc123");

        Assert.NotNull(loaded);
        Assert.Equal(snap.Id, loaded!.Id);
        Assert.Equal(snap.ServiceName, loaded.ServiceName);
        Assert.Equal(snap.PreviousState, loaded.PreviousState);
    }

    [Fact]
    public async Task List_ReturnsAllSavedIds()
    {
        for (int i = 0; i < 3; i++)
        {
            var snap = new Snapshot($"id{i}", DateTime.UtcNow, "svc:x", "Service", "Running", "Stopped");
            await _store.SaveAsync(snap);
        }

        var ids = _store.List().ToList();
        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public async Task LoadAsync_ReturnsNullForMissingId()
    {
        var result = await _store.LoadAsync("nonexistent");
        Assert.Null(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
