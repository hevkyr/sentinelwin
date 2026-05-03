using System.Windows;
using SentinelWin.UI.ViewModels;

namespace SentinelWin.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
        => await _vm.ScanAsync();

    private async void ApplyBtn_Click(object sender, RoutedEventArgs e)
    {
        _vm.SelectedItem = ResultsGrid.SelectedItem as Core.Models.ScanItem;
        await _vm.ApplySelectedAsync();
    }

    private async void RollbackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (SnapshotList.SelectedItem is string snapId)
            await _vm.RollbackAsync(snapId);
    }

    private void ResultsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _vm.SelectedItem = ResultsGrid.SelectedItem as Core.Models.ScanItem;
    }
}
