namespace SentinelWin.Core.Models;

/// <summary>
/// Immutable record representing a single audit finding.
/// </summary>
/// <param name="IsCompliant">True when the current state matches the secure/expected state.</param>
/// <param name="ExpectedValue">The value considered secure for this item (null = presence check).</param>
public record ScanItem(
    string Id,
    string Name,
    string Type,            // Service | Task | Registry | Process | Network
    string Description,
    string Status,
    Risk Risk,
    string Recommendation,
    bool IsCompliant = false,
    string? ExpectedValue = null
);
