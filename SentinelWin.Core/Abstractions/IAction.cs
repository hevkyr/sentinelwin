using SentinelWin.Core.Models;

namespace SentinelWin.Core.Abstractions;

/// <summary>
/// Represents a reversible system change that can be applied to a ScanItem
/// and rolled back from a Snapshot.
/// </summary>
public interface IAction
{
    string Name { get; }

    /// <summary>Returns true if this action can handle the given item's Type.</summary>
    bool CanHandle(ScanItem item);

    /// <summary>
    /// Applies the action. If <paramref name="dryRun"/> is true, no system changes
    /// are made and a descriptive message is returned.
    /// </summary>
    Task<ActionResult> ApplyAsync(ScanItem item, bool dryRun, CancellationToken ct = default);

    /// <summary>Reverts a previously applied action using its saved snapshot.</summary>
    Task<ActionResult> RollbackAsync(Snapshot snapshot, CancellationToken ct = default);
}
