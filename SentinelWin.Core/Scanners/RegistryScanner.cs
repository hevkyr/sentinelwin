using Microsoft.Win32;
using SentinelWin.Core.Abstractions;
using SentinelWin.Core.Models;

namespace SentinelWin.Core.Scanners;

/// <summary>
/// Scans known Windows registry keys that control telemetry, privacy, and diagnostic settings.
/// BUG FIX: Original used wrong registry path for AllowTelemetry.
/// </summary>
public sealed class RegistryScanner : IScanner
{
    public string Name => "Registry Scanner";

    private static readonly (
        RegistryHive Hive,
        string SubKey,
        string ValueName,
        string FriendlyName,
        string Description,
        object? ExpectedValue,
        Risk Risk,
        string Recommendation)[] Targets =
    {
        // GP-controlled telemetry path (takes precedence on domain machines)
        (
            RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            "AllowTelemetry",
            "AllowTelemetry (Group Policy)",
            @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            0, Risk.High,
            "Set to 0 (Security) — disables telemetry via Group Policy."
        ),
        // Settings-app path (Home/Pro)
        (
            RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
            "AllowTelemetry",
            "AllowTelemetry (Settings)",
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
            0, Risk.High,
            "Set to 0 to minimise telemetry level."
        ),
        (
            RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            "DoNotShowFeedbackNotifications",
            "Feedback Notifications",
            "Controls Windows Feedback solicitation prompts.",
            1, Risk.Low,
            "Set to 1 to suppress feedback prompts."
        ),
        (
            RegistryHive.CurrentUser,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
            "Enabled",
            "Advertising ID",
            "Per-user unique ID used for cross-app ad targeting.",
            0, Risk.Medium,
            "Set to 0 to disable the Advertising ID."
        ),
        (
            RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\AppCompat",
            "DisableInventory",
            "Application Inventory Upload",
            "Controls whether installed-software inventory is sent to Microsoft.",
            1, Risk.Medium,
            "Set to 1 to block inventory telemetry."
        ),
    };

    public Task<IReadOnlyList<ScanItem>> ScanAsync(CancellationToken ct = default)
    {
        var list = new List<ScanItem>();

        foreach (var t in Targets)
        {
            ct.ThrowIfCancellationRequested();

            object? actual = null;
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(t.Hive, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(t.SubKey);
                actual = key?.GetValue(t.ValueName);
            }
            catch { /* access denied or key absent — treat as Not Set */ }

            bool compliant = actual is not null
                && actual.ToString() == t.ExpectedValue?.ToString();

            list.Add(new ScanItem(
                Id: $"reg:{t.Hive}:{t.SubKey}:{t.ValueName}",
                Name: t.FriendlyName,
                Type: "Registry",
                Description: t.Description,
                Status: actual is null ? "Not Set" : actual.ToString()!,
                Risk: compliant ? Risk.Low : t.Risk,
                Recommendation: t.Recommendation,
                IsCompliant: compliant,
                ExpectedValue: t.ExpectedValue?.ToString()
            ));
        }

        return Task.FromResult<IReadOnlyList<ScanItem>>(list);
    }
}
