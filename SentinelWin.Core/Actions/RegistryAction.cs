using Microsoft.Win32;
using SentinelWin.Core.Abstractions;
using SentinelWin.Core.Models;
using SentinelWin.Core.Services;

namespace SentinelWin.Core.Actions;

/// <summary>
/// Applies and rolls back registry value changes for telemetry/privacy hardening.
/// Missing from original project — RegistryScanner had no corresponding action.
/// </summary>
public sealed class RegistryAction : IAction
{
    private readonly SnapshotStore _store;

    public RegistryAction(SnapshotStore store)
    {
        _store = store;
    }

    public string Name => "Set Registry Value";
    public bool CanHandle(ScanItem item) => item.Type == "Registry";

    public async Task<ActionResult> ApplyAsync(ScanItem item, bool dryRun, CancellationToken ct = default)
    {
        // Id format: "reg:LocalMachine:SOFTWARE\...:ValueName"
        var parts = item.Id.Split(':', 4);
        if (parts.Length < 4)
            return new ActionResult(false, $"Cannot parse registry Id: {item.Id}");

        if (!Enum.TryParse<RegistryHive>(parts[1], out var hive))
            return new ActionResult(false, $"Unknown hive: {parts[1]}");

        var subKey  = parts[2];
        var valName = parts[3];

        if (item.ExpectedValue is null)
            return new ActionResult(false, "No expected value defined for this registry item.");

        if (dryRun)
            return new ActionResult(true,
                $"[DRY-RUN] Would set HKLM\\{subKey}\\{valName} = {item.ExpectedValue}");

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.CreateSubKey(subKey, writable: true);

            // Capture current value for rollback
            var previous = key.GetValue(valName);
            var previousStr = previous?.ToString() ?? "<not set>";

            // Write the new value — treat as DWORD if it parses as int
            if (int.TryParse(item.ExpectedValue, out int intVal))
                key.SetValue(valName, intVal, RegistryValueKind.DWord);
            else
                key.SetValue(valName, item.ExpectedValue, RegistryValueKind.String);

            var snap = new Snapshot(
                Id: Guid.NewGuid().ToString("N"),
                CreatedAt: DateTime.UtcNow,
                ItemId: item.Id,
                Type: "Registry",
                PreviousState: previousStr,
                NewState: item.ExpectedValue,
                RegistryHive: parts[1],
                RegistrySubKey: subKey,
                RegistryValueName: valName
            );

            await _store.SaveAsync(snap, ct);
            return new ActionResult(true,
                $"Set {valName} = {item.ExpectedValue} (was {previousStr}).", snap.Id);
        }
        catch (UnauthorizedAccessException)
        {
            return new ActionResult(false,
                $"Access denied. Run SentinelWin as Administrator to modify {subKey}.");
        }
        catch (Exception ex)
        {
            return new ActionResult(false, $"Registry write failed: {ex.Message}");
        }
    }

    public Task<ActionResult> RollbackAsync(Snapshot snapshot, CancellationToken ct = default)
    {
        if (snapshot.RegistryHive is null || snapshot.RegistrySubKey is null || snapshot.RegistryValueName is null)
            return Task.FromResult(new ActionResult(false, "Snapshot missing registry fields for rollback."));

        if (!Enum.TryParse<RegistryHive>(snapshot.RegistryHive, out var hive))
            return Task.FromResult(new ActionResult(false, $"Unknown hive in snapshot: {snapshot.RegistryHive}"));

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(snapshot.RegistrySubKey, writable: true);

            if (key is null)
                return Task.FromResult(new ActionResult(false, $"Registry key not found: {snapshot.RegistrySubKey}"));

            if (snapshot.PreviousState == "<not set>")
                key.DeleteValue(snapshot.RegistryValueName, throwOnMissingValue: false);
            else if (int.TryParse(snapshot.PreviousState, out int prev))
                key.SetValue(snapshot.RegistryValueName, prev, RegistryValueKind.DWord);
            else
                key.SetValue(snapshot.RegistryValueName, snapshot.PreviousState, RegistryValueKind.String);

            return Task.FromResult(new ActionResult(true,
                $"Restored {snapshot.RegistryValueName} to '{snapshot.PreviousState}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ActionResult(false, $"Registry rollback failed: {ex.Message}"));
        }
    }
}
