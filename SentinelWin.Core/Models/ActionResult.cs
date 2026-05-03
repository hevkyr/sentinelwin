namespace SentinelWin.Core.Models;

/// <param name="SnapshotId">ID of the persisted snapshot, if one was created.</param>
public record ActionResult(
    bool Success,
    string Message,
    string? SnapshotId = null
);
