using System.ServiceProcess;
using SentinelWin.Core.Abstractions;
using SentinelWin.Core.Models;

namespace SentinelWin.Core.Scanners;

/// <summary>
/// Detects Windows services associated with telemetry, diagnostics, and data collection.
/// </summary>
public sealed class ServiceScanner : IScanner
{
    public string Name => "Service Scanner";

    private static readonly (string Svc, Risk Risk, string Desc, string Reco)[] Targets =
    {
        ("DiagTrack",        Risk.High,   "Connected User Experiences and Telemetry — uploads diagnostic data to Microsoft.",  "Disable: stops background telemetry uploads."),
        ("dmwappushservice", Risk.Medium, "WAP Push Message Routing — routes push messages; also used by telemetry infrastructure.", "Safe to disable on non-cellular PCs."),
        ("WMPNetworkSvc",    Risk.Low,    "Windows Media Player Network Sharing — shares media libraries on local network.",   "Disable if not sharing media via WMP."),
        ("WerSvc",           Risk.Medium, "Windows Error Reporting — sends crash dumps and diagnostic data to Microsoft.",     "Disable to prevent crash data uploads."),
        ("PcaSvc",           Risk.Low,    "Program Compatibility Assistant — monitors app compatibility; feeds telemetry.",    "Disable if application compatibility warnings are not needed."),
        ("SysMain",          Risk.Low,    "Superfetch/SysMain — prefetches apps into RAM; may also gather usage statistics.",  "Optional: disable on SSDs where prefetching gives no benefit."),
    };

    public Task<IReadOnlyList<ScanItem>> ScanAsync(CancellationToken ct = default)
    {
        var list = new List<ScanItem>();
        foreach (var (svc, risk, desc, reco) in Targets)
        {
            ct.ThrowIfCancellationRequested();
            string status = "Not Found";
            bool compliant = false;
            try
            {
                using var sc = new ServiceController(svc);
                status = sc.Status.ToString();
                compliant = sc.Status is ServiceControllerStatus.Stopped;
            }
            catch { compliant = true; /* service absent = not a threat */ }

            list.Add(new ScanItem(
                Id: $"svc:{svc}",
                Name: svc,
                Type: "Service",
                Description: desc,
                Status: status,
                Risk: compliant ? Risk.Low : risk,
                Recommendation: reco,
                IsCompliant: compliant
            ));
        }
        return Task.FromResult<IReadOnlyList<ScanItem>>(list);
    }
}
