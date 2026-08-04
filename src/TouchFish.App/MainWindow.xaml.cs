using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using System.Windows.Interop;
using TouchFish.Modules.BossKey;
using TouchFish.Modules.Reader;

namespace TouchFish.App;

public partial class MainWindow : FluentWindow
{
    private readonly BossKeyViewModel _bossKeyViewModel;
    private readonly BossKeyView _bossKeyView;
    private readonly ReaderView _readerView;
    private readonly SettingsView _settingsView;

    public MainWindow(
        BossKeyViewModel bossKeyViewModel,
        ReaderViewModel readerViewModel,
        SettingsViewModel settingsViewModel)
    {
        _bossKeyViewModel = bossKeyViewModel;
        DataContext = settingsViewModel;
        InitializeComponent();
        _bossKeyView = new BossKeyView { DataContext = bossKeyViewModel };
        _readerView = new ReaderView { DataContext = readerViewModel };
        _settingsView = new SettingsView { DataContext = settingsViewModel };
        PageHost.Content = _bossKeyView;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _bossKeyViewModel.AttachHotkey(handle);
    }

    private void Navigation_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || PrimaryNavigation.SelectedIndex < 0)
        {
            return;
        }

        SettingsNavigation.SelectedIndex = -1;
        PageHost.Content = PrimaryNavigation.SelectedIndex == 0 ? _bossKeyView : _readerView;
    }

    private void SettingsNavigation_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || SettingsNavigation.SelectedIndex < 0)
        {
            return;
        }

        PrimaryNavigation.SelectedIndex = -1;
        PageHost.Content = _settingsView;
    }
}
