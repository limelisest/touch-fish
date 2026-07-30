using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TouchFish.Contracts;
using TouchFish.Modules.BossKey;
using TouchFish.Platform.Windows;

namespace TouchFish.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private TrayIconService? _trayIcon;
    private bool _exitRequested;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder(e.Args);
        builder.Services.AddSingleton<IHotkeyService, Win32HotkeyService>();
        builder.Services.AddSingleton<IWindowService, Win32WindowService>();
        builder.Services.AddBossKeyModule();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        await _host.StartAsync();

        var viewModel = _host.Services.GetRequiredService<BossKeyViewModel>();
        await viewModel.InitializeAsync();

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Closing += (_, args) =>
        {
            if (_exitRequested)
            {
                return;
            }

            args.Cancel = true;
            window.Hide();
        };
        _trayIcon = new TrayIconService(window, () =>
        {
            _exitRequested = true;
            Shutdown();
        });
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();

        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
