using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TouchFish.Contracts;
using TouchFish.Modules.BossKey;
using TouchFish.Modules.Reader;
using TouchFish.Platform.Windows;

namespace TouchFish.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private SystemThemeService? _themeService;
    private TrayIconService? _trayIcon;
    private ReaderWindowManager? _readerWindowManager;
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
            PrepareLogDirectory();
            _themeService = new SystemThemeService(this);
            _themeService.Start();

            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddSingleton<IHotkeyService, Win32HotkeyService>();
            builder.Services.AddSingleton<IWindowService, Win32WindowService>();
            builder.Services.AddSingleton<IWindowPickerService, Win32WindowPickerService>();
            builder.Services.AddSingleton<IToolWindowRegistry, ToolWindowRegistry>();
            builder.Services.AddBossKeyModule();
            builder.Services.AddReaderModule();
            builder.Services.AddSingleton<AppSettingsStore>();
            builder.Services.AddSingleton<StartupTaskService>();
            builder.Services.AddSingleton<SettingsViewModel>();
            builder.Services.AddSingleton<MainWindow>();

            _host = builder.Build();
            await _host.StartAsync();

            var bossKeyViewModel = _host.Services.GetRequiredService<BossKeyViewModel>();
            var readerViewModel = _host.Services.GetRequiredService<ReaderViewModel>();
            _readerWindowManager = _host.Services.GetRequiredService<ReaderWindowManager>();
            var settingsViewModel = _host.Services.GetRequiredService<SettingsViewModel>();
            await bossKeyViewModel.InitializeAsync();
            await readerViewModel.InitializeAsync();
            await settingsViewModel.InitializeAsync();

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
                PrepareReaderShutdown();
                Shutdown();
            });
            var silent = e.Args.Any(argument =>
                string.Equals(argument, "--silent", StringComparison.OrdinalIgnoreCase));
            if (silent)
            {
                // Create the native handle without showing the main window so the global hotkey works.
                _ = new WindowInteropHelper(window).EnsureHandle();
            }
            else
            {
                window.Show();
            }
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
        PrepareReaderShutdown();
        Shutdown(-1);
    }

    private static string WriteCrashLog(Exception exception)
    {
        try
        {
            var directory = GetLogDirectory();
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
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "LimeLisest",
                "TouchFish",
                "log");
        }
    }

    private static string GetLogDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "LimeLisest",
        "TouchFish",
        "log");

    private static void PrepareLogDirectory()
    {
        var logDirectory = GetLogDirectory();
        try
        {
            Directory.CreateDirectory(logDirectory);
            var legacyDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TouchFish",
                "logs");
            if (!Directory.Exists(legacyDirectory))
            {
                return;
            }

            foreach (var legacyLog in Directory.EnumerateFiles(legacyDirectory, "*.log"))
            {
                try
                {
                    var destination = Path.Combine(logDirectory, Path.GetFileName(legacyLog));
                    if (!File.Exists(destination))
                    {
                        File.Move(legacyLog, destination);
                    }
                }
                catch
                {
                    // A locked legacy log can remain in place without blocking startup.
                }
            }
        }
        catch
        {
            // Logging must never prevent TouchFish from starting.
        }
    }

    private void PrepareReaderShutdown()
    {
        try
        {
            _readerWindowManager?.PrepareForShutdown();
        }
        catch
        {
            // Shutdown must continue even if a tool window has already been destroyed by WPF.
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        PrepareReaderShutdown();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        PrepareReaderShutdown();
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
