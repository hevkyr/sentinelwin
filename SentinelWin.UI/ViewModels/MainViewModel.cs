using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using SentinelWin.Core.Abstractions;
using SentinelWin.Core.Actions;
using SentinelWin.Core.Models;
using SentinelWin.Core.Scanners;
using SentinelWin.Core.Services;

namespace SentinelWin.UI.ViewModels;

/// <summary>
/// Main ViewModel — separates UI logic from the MainWindow code-behind.
/// Wires up all scanners and actions so the UI can actually apply fixes.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ScanEngine _engine;
    private readonly SnapshotStore _snapshots;

    private int _score = 0;
    private bool _isBusy = false;
    private string _statusText = "Ready — click Scan to start.";
    private bool _isDryRun = true;

    public ObservableCollection<ScanItem> Items { get; } = [];
    public ObservableCollection<string> SnapshotIds { get; } = [];
    public ObservableCollection<ActionLogEntry> ActionLog { get; } = [];

    public int Score
    {
        get => _score;
        set { _score = value; OnPropertyChanged(); OnPropertyChanged(nameof(ScoreLabel)); }
    }

    public string ScoreLabel => _score == 0 && !Items.Any() ? "—" : $"{_score} / 100";

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsIdle => !_isBusy;

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public bool IsDryRun
    {
        get => _isDryRun;
        set { _isDryRun = value; OnPropertyChanged(); }
    }

    public ScanItem? SelectedItem { get; set; }

    public MainViewModel()
    {
        _snapshots = new SnapshotStore();

        var store = _snapshots;
        var scanners = new IScanner[]
        {
            new ServiceScanner(),
            new RegistryScanner(),
            new TaskScanner(),
        };
        var actions = new IAction[]
        {
            new ServiceAction(store),
            new RegistryAction(store),
            new TaskAction(store),
        };

        _engine = new ScanEngine(scanners, actions);
        RefreshSnapshots();
    }

    public async Task ScanAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        StatusText = "Scanning...";
        Items.Clear();
        try
        {
            var results = await _engine.RunAllAsync(ct);
            foreach (var item in results) Items.Add(item);
            Score = ScanEngine.PrivacyScore(results);
            StatusText = $"Scan complete — {results.Count} items, score {Score}/100.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan error: {ex.Message}";
            MessageBox.Show(ex.Message, "Scan Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ApplySelectedAsync(CancellationToken ct = default)
    {
        if (SelectedItem is null) return;

        IsBusy = true;
        StatusText = $"Applying fix for '{SelectedItem.Name}'...";
        try
        {
            var result = await _engine.ApplyActionAsync(SelectedItem, IsDryRun, ct);
            if (result is null)
            {
                StatusText = $"No action available for type '{SelectedItem.Type}'.";
                return;
            }

            ActionLog.Insert(0, new ActionLogEntry(DateTime.Now, SelectedItem.Name, result));

            if (result.Success)
            {
                StatusText = result.Message;
                RefreshSnapshots();
                // Re-scan to update the grid
                await ScanAsync(ct);
            }
            else
            {
                StatusText = $"Failed: {result.Message}";
                MessageBox.Show(result.Message, "Action Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RollbackAsync(string snapshotId, CancellationToken ct = default)
    {
        var snap = await _snapshots.LoadAsync(snapshotId, ct);
        if (snap is null)
        {
            MessageBox.Show($"Snapshot '{snapshotId}' not found.", "Rollback", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        StatusText = $"Rolling back snapshot '{snapshotId}'...";
        try
        {
            var result = await _engine.RollbackAsync(snap, ct);
            if (result is null)
            {
                StatusText = "No action found for this snapshot type.";
                return;
            }
            ActionLog.Insert(0, new ActionLogEntry(DateTime.Now, $"ROLLBACK: {snap.ItemId}", result));
            StatusText = result.Success ? $"Rollback successful: {result.Message}" : $"Rollback failed: {result.Message}";
            await ScanAsync(ct);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshSnapshots()
    {
        SnapshotIds.Clear();
        foreach (var id in _snapshots.List().OrderByDescending(x => x))
            SnapshotIds.Add(id);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public record ActionLogEntry(DateTime Timestamp, string ItemName, ActionResult Result)
{
    public string Display => $"[{Timestamp:HH:mm:ss}] {(Result.Success ? "✓" : "✗")} {ItemName}: {Result.Message}";
}
