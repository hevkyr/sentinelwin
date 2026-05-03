namespace SentinelWin.Core.Models;

/// <summary>
/// Captures the state of a system item before a change is applied,
/// enabling full rollback without ambiguity.
/// </summary>
/// <param name="ServiceName">
///   For Service snapshots: the actual SCM service name (e.g. "DiagTrack").
///   BUG FIX: Original stored previous status string in PreviousState and tried to
///   use it as the service name in RollbackAsync — now we store the name explicitly.
/// </param>
public record Snapshot(
    string Id,
    DateTime CreatedAt,
    string ItemId,
    string Type,
    string PreviousState,   // human-readable previous status / value
    string NewState,        // human-readable new status / value
    string? ServiceName = null,     // Service rollback: SCM name
    string? RegistryHive = null,    // Registry rollback: hive name
    string? RegistrySubKey = null,  // Registry rollback: subkey path
    string? RegistryValueName = null // Registry rollback: value name
);
