using SentinelWin.Core.Abstractions;
using SentinelWin.Core.Models;

namespace SentinelWin.Core.Services;

public sealed class ScanEngine
{
    private readonly IReadOnlyList<IScanner> _scanners;
    private readonly IReadOnlyList<IAction> _actions;

    public ScanEngine(IEnumerable<IScanner> scanners, IEnumerable<IAction>? actions = null)
    {
        _scanners = scanners.ToList();
        _actions = actions?.ToList() ?? [];
    }

    public async Task<IReadOnlyList<ScanItem>> RunAllAsync(CancellationToken ct = default)
    {
        var all = new List<ScanItem>();
        foreach (var s in _scanners)
        {
            ct.ThrowIfCancellationRequested();
            all.AddRange(await s.ScanAsync(ct));
        }
        return all;
    }

    /// <summary>
    /// Applies the appropriate action for a given ScanItem.
    /// Returns null if no registered action handles this item type.
    /// </summary>
    public async Task<ActionResult?> ApplyActionAsync(
        ScanItem item, bool dryRun, CancellationToken ct = default)
    {
        var action = _actions.FirstOrDefault(a => a.CanHandle(item));
        if (action is null) return null;
        return await action.ApplyAsync(item, dryRun, ct);
    }

    /// <summary>
    /// Rolls back a previously applied action using its saved snapshot.
    /// </summary>
    public async Task<ActionResult?> RollbackAsync(
        Snapshot snapshot, CancellationToken ct = default)
    {
        var action = _actions.FirstOrDefault(a => a.Name ==
            snapshot.Type switch
            {
                "Service"  => "Disable Service",
                "Registry" => "Set Registry Value",
                "Task"     => "Disable Scheduled Task",
                _ => string.Empty
            });
        if (action is null) return null;
        return await action.RollbackAsync(snapshot, ct);
    }

    public static int PrivacyScore(IEnumerable<ScanItem> items)
    {
        static bool IsActive(ScanItem i) => !i.IsCompliant &&
            i.Status is not ("Stopped" or "Disabled" or "Not Found" or "Not Set" or "NotFound");

        int score = 100;
        foreach (var i in items.Where(IsActive))
        {
            score -= i.Risk switch
            {
                Risk.High   => 15,
                Risk.Medium => 8,
                Risk.Low    => 2,
                _           => 0
            };
        }
        return Math.Max(0, score);
    }
}
