using System.ServiceProcess;
using SentinelWin.Core.Abstractions;
using SentinelWin.Core.Models;
using SentinelWin.Core.Services;

namespace SentinelWin.Core.Actions;

/// <summary>
/// Stops and disables a Windows service with snapshot-based rollback.
///
/// BUG FIXES vs original:
///   1. RollbackAsync was using PreviousState (which held the status string "Running")
///      as the service name — now ServiceName field is used unambiguously.
///   2. ApplyAsync generated a snapId but never actually persisted the snapshot.
///      Now it writes via SnapshotStore.
///   3. StartupType is now set to Disabled on apply, and restored on rollback.
/// </summary>
public sealed class ServiceAction : IAction
{
    private readonly SnapshotStore _store;

    public ServiceAction(SnapshotStore store)
    {
        _store = store;
    }

    public string Name => "Disable Service";
    public bool CanHandle(ScanItem item) => item.Type == "Service";

    public async Task<ActionResult> ApplyAsync(ScanItem item, bool dryRun, CancellationToken ct = default)
    {
        // Extract the raw service name from the Id ("svc:DiagTrack" → "DiagTrack")
        var svcName = item.Id.StartsWith("svc:", StringComparison.Ordinal)
            ? item.Id[4..]
            : item.Name;

        if (dryRun)
            return new ActionResult(true, $"[DRY-RUN] Would stop and disable service '{svcName}'.");

        try
        {
            using var sc = new ServiceController(svcName);
            string previousStatus = sc.Status.ToString();
            string previousStartType = GetStartupType(svcName);

            if (sc.Status == ServiceControllerStatus.Running)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
            }

            SetStartupType(svcName, "Disabled");

            var snap = new Snapshot(
                Id: Guid.NewGuid().ToString("N"),
                CreatedAt: DateTime.UtcNow,
                ItemId: item.Id,
                Type: "Service",
                PreviousState: $"{previousStatus}/{previousStartType}",
                NewState: "Stopped/Disabled",
                ServiceName: svcName   // stored explicitly for unambiguous rollback
            );

            await _store.SaveAsync(snap, ct);
            return new ActionResult(true, $"Service '{svcName}' stopped and disabled (was {previousStatus}/{previousStartType}).", snap.Id);
        }
        catch (Exception ex)
        {
            return new ActionResult(false, $"Failed to stop '{svcName}': {ex.Message}");
        }
    }

    public async Task<ActionResult> RollbackAsync(Snapshot snapshot, CancellationToken ct = default)
    {
        // BUG FIX: use snapshot.ServiceName, not PreviousState, for the SCM name.
        var svcName = snapshot.ServiceName
            ?? (snapshot.ItemId.StartsWith("svc:", StringComparison.Ordinal)
                ? snapshot.ItemId[4..]
                : snapshot.ItemId);

        try
        {
            // Restore startup type first (extract from "Running/Automatic" format)
            var parts = snapshot.PreviousState.Split('/');
            var previousStartType = parts.Length > 1 ? parts[1] : "Manual";
            SetStartupType(svcName, previousStartType);

            using var sc = new ServiceController(svcName);
            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
            }

            return new ActionResult(true, $"Service '{sc.ServiceName}' restored to {snapshot.PreviousState}.");
        }
        catch (Exception ex)
        {
            return new ActionResult(false, $"Rollback failed for '{svcName}': {ex.Message}");
        }
    }

    private static string GetStartupType(string serviceName)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            var start = key?.GetValue("Start");
            return start is int s ? s switch { 2 => "Automatic", 3 => "Manual", 4 => "Disabled", _ => s.ToString() } : "Unknown";
        }
        catch { return "Unknown"; }
    }

    private static void SetStartupType(string serviceName, string startupType)
    {
        // Use sc.exe for reliability — WMI/P/Invoke requires additional privileges
        var scArgs = startupType.ToLowerInvariant() switch
        {
            "disabled" => $"config \"{serviceName}\" start= disabled",
            "manual"   => $"config \"{serviceName}\" start= demand",
            "automatic" or "auto" => $"config \"{serviceName}\" start= auto",
            _ => null
        };
        if (scArgs is null) return;

        using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = scArgs,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        proc?.WaitForExit(5000);
    }
}
