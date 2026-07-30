using System.Windows;
using System.Windows.Interop;
using TouchFish.Modules.BossKey;

namespace TouchFish.App;

public partial class MainWindow : Window
{
    private readonly BossKeyViewModel _viewModel;

    public MainWindow(BossKeyViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _viewModel.AttachHotkey(handle);
    }
}
