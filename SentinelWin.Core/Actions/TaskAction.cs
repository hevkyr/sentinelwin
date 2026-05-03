using SentinelWin.Core.Abstractions;
using SentinelWin.Core.Models;
using SentinelWin.Core.Services;

namespace SentinelWin.Core.Actions;

/// <summary>
/// Enables and disables scheduled tasks with snapshot-based rollback.
/// Added — counterpart to TaskScanner, was missing from original.
/// </summary>
public sealed class TaskAction : IAction
{
    private readonly SnapshotStore _store;

    public TaskAction(SnapshotStore store)
    {
        _store = store;
    }

    public string Name => "Disable Scheduled Task";
    public bool CanHandle(ScanItem item) => item.Type == "Task";

    public async Task<ActionResult> ApplyAsync(ScanItem item, bool dryRun, CancellationToken ct = default)
    {
        var taskPath = item.Id.StartsWith("task:", StringComparison.Ordinal)
            ? item.Id[5..]
            : item.Name;

        if (dryRun)
            return new ActionResult(true, $"[DRY-RUN] Would disable task '{taskPath}'.");

        var (exitCode, output) = await RunSchtasksAsync($"/Change /TN \"{taskPath}\" /DISABLE");

        if (exitCode != 0)
            return new ActionResult(false, $"schtasks /DISABLE failed (exit {exitCode}): {output}");

        var snap = new Snapshot(
            Id: Guid.NewGuid().ToString("N"),
            CreatedAt: DateTime.UtcNow,
            ItemId: item.Id,
            Type: "Task",
            PreviousState: item.Status,
            NewState: "Disabled"
        );
        await _store.SaveAsync(snap, ct);
        return new ActionResult(true, $"Task '{taskPath}' disabled (was {item.Status}).", snap.Id);
    }

    public async Task<ActionResult> RollbackAsync(Snapshot snapshot, CancellationToken ct = default)
    {
        var taskPath = snapshot.ItemId.StartsWith("task:", StringComparison.Ordinal)
            ? snapshot.ItemId[5..]
            : snapshot.ItemId;

        var (exitCode, output) = await RunSchtasksAsync($"/Change /TN \"{taskPath}\" /ENABLE");
        return exitCode == 0
            ? new ActionResult(true, $"Task '{taskPath}' re-enabled.")
            : new ActionResult(false, $"Failed to re-enable '{taskPath}' (exit {exitCode}): {output}");
    }

    private static async Task<(int ExitCode, string Output)> RunSchtasksAsync(string args)
    {
        using var proc = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };
        proc.Start();
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, (stdout + stderr).Trim());
    }
}
