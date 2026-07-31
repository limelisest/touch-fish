using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TouchFish.Contracts;
using TouchFish.Modules.BossKey;
using TouchFish.Platform.Windows;

namespace TouchFish.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private SystemThemeService? _themeService;
    private TrayIconService? _trayIcon;
    private bool _exitRequested;
    private int _fatalReported;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteCrashLog(args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            args.SetObserved();
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _themeService = new SystemThemeService(this);
            _themeService.Start();

            var builder = Host.CreateApplicationBuilder(e.Args);
            builder.Services.AddSingleton<IHotkeyService, Win32HotkeyService>();
            builder.Services.AddSingleton<IWindowService, Win32WindowService>();
            builder.Services.AddSingleton<IWindowPickerService, Win32WindowPickerService>();
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
        catch (Exception exception)
        {
            ReportFatalError(exception);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ReportFatalError(e.Exception);
    }

    private void ReportFatalError(Exception exception)
    {
        var logPath = WriteCrashLog(exception);
        if (Interlocked.Exchange(ref _fatalReported, 1) != 0)
        {
            return;
        }

        System.Windows.MessageBox.Show(
            $"TouchFish 启动失败。\n\n错误日志已保存到：\n{logPath}\n\n请把日志发给开发者。",
            "TouchFish 启动失败",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        _exitRequested = true;
        Shutdown(-1);
    }

    private static string WriteCrashLog(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TouchFish",
                "logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var content = new StringBuilder()
                .AppendLine($"TouchFish {typeof(App).Assembly.GetName().Version}")
                .AppendLine($"Time: {DateTimeOffset.Now:O}")
                .AppendLine($"OS: {Environment.OSVersion}")
                .AppendLine($"Runtime: {Environment.Version}")
                .AppendLine($"64-bit process: {Environment.Is64BitProcess}")
                .AppendLine()
                .AppendLine(exception.ToString())
                .ToString();
            File.WriteAllText(path, content, Encoding.UTF8);
            return path;
        }
        catch
        {
            return "%LocalAppData%\\TouchFish\\logs";
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _themeService?.Dispose();

        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
