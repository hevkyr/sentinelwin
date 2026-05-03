using Moq;
using SentinelWin.Core.Abstractions;
using SentinelWin.Core.Models;
using SentinelWin.Core.Services;
using Xunit;

namespace SentinelWin.Tests;

public class ScanEngineTests
{
    [Fact]
    public async Task RunAllAsync_AggregatesResultsFromAllScanners()
    {
        var s1 = new Mock<IScanner>();
        s1.Setup(s => s.ScanAsync(It.IsAny<CancellationToken>()))
          .ReturnsAsync(new List<ScanItem>
          {
              new("svc:foo", "Foo", "Service", "desc", "Running", Risk.High, "rec")
          });

        var s2 = new Mock<IScanner>();
        s2.Setup(s => s.ScanAsync(It.IsAny<CancellationToken>()))
          .ReturnsAsync(new List<ScanItem>
          {
              new("reg:bar", "Bar", "Registry", "desc", "1", Risk.Medium, "rec")
          });

        var engine = new ScanEngine(new[] { s1.Object, s2.Object });
        var results = await engine.RunAllAsync();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task RunAllAsync_RespectsCtCancellation()
    {
        var slow = new Mock<IScanner>();
        slow.Setup(s => s.ScanAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                await Task.Delay(5000, ct);
                return (IReadOnlyList<ScanItem>)new List<ScanItem>();
            });

        using var cts = new CancellationTokenSource(50);
        var engine = new ScanEngine(new[] { slow.Object });

        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.RunAllAsync(cts.Token));
    }

    [Theory]
    [InlineData("Running",  Risk.High,   15)]
    [InlineData("Running",  Risk.Medium, 8)]
    [InlineData("Running",  Risk.Low,    2)]
    [InlineData("Stopped",  Risk.High,   0)]   // stopped → no penalty
    [InlineData("Disabled", Risk.High,   0)]   // disabled → no penalty
    [InlineData("Not Found",Risk.High,   0)]   // absent → no penalty
    public void PrivacyScore_PenalisesCorrectly(string status, Risk risk, int expectedDeduction)
    {
        var item = new ScanItem("id", "name", "Service", "desc", status, risk, "rec",
            IsCompliant: status is "Stopped" or "Disabled" or "Not Found");
        var score = ScanEngine.PrivacyScore(new[] { item });
        Assert.Equal(100 - expectedDeduction, score);
    }

    [Fact]
    public void PrivacyScore_NeverGoesBelowZero()
    {
        var items = Enumerable.Repeat(
            new ScanItem("id", "name", "Service", "desc", "Running", Risk.High, "rec"),
            20
        );
        Assert.Equal(0, ScanEngine.PrivacyScore(items));
    }
}
