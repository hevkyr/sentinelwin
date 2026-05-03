using System.Xml.Linq;
using SentinelWin.Core.Abstractions;
using SentinelWin.Core.Models;

namespace SentinelWin.Core.Scanners;

/// <summary>
/// Detects enabled scheduled tasks associated with telemetry and CEIP programs.
/// Added — was listed in README roadmap but not implemented.
/// </summary>
public sealed class TaskScanner : IScanner
{
    public string Name => "Task Scheduler Scanner";

    private static readonly (string TaskPath, string FriendlyName, Risk Risk, string Description)[] Targets =
    {
        (
            @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
            "CEIP Consolidator",
            Risk.High,
            "Collects and uploads usage statistics to Microsoft's CEIP program."
        ),
        (
            @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
            "USB CEIP",
            Risk.Medium,
            "Sends USB device telemetry to Microsoft."
        ),
        (
            @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
            "Compatibility Appraiser",
            Risk.Medium,
            "Inventories installed software for compatibility and telemetry purposes."
        ),
        (
            @"\Microsoft\Windows\Application Experience\AitAgent",
            "AIT Agent",
            Risk.Medium,
            "Application Impact Telemetry agent — reports app usage metrics."
        ),
        (
            @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
            "Disk Diagnostic Data Collector",
            Risk.Low,
            "Collects disk health data; uploads to Microsoft if CEIP is enabled."
        ),
        (
            @"\Microsoft\Windows\PI\Sqm-Tasks",
            "SQM Tasks",
            Risk.Medium,
            "Software Quality Metrics tasks — report feature usage data."
        ),
    };

    public async Task<IReadOnlyList<ScanItem>> ScanAsync(CancellationToken ct = default)
    {
        var list = new List<ScanItem>();

        // Use schtasks.exe to query each task — avoids COM/TaskScheduler NuGet dependency
        foreach (var (taskPath, friendlyName, risk, desc) in Targets)
        {
            ct.ThrowIfCancellationRequested();
            var state = await QueryTaskStateAsync(taskPath);

            list.Add(new ScanItem(
                Id: $"task:{taskPath}",
                Name: friendlyName,
                Type: "Task",
                Description: desc,
                Status: state,
                Risk: state == "Disabled" || state == "Not Found" ? Risk.Low : risk,
                Recommendation: "Disable this task to prevent telemetry data collection.",
                IsCompliant: state is "Disabled" or "Not Found"
            ));
        }

        return list;
    }

    private static async Task<string> QueryTaskStateAsync(string taskPath)
    {
        try
        {
            using var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Query /TN \"{taskPath}\" /FO CSV /NH",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            proc.Start();
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
                return "Not Found";

            // CSV format: "TaskName","Next Run Time","Status"
            var parts = stdout.Trim().Split(',');
            var status = parts.Length >= 3 ? parts[2].Trim('"', ' ') : "Unknown";
            return status;
        }
        catch
        {
            return "Unknown";
        }
    }
}
