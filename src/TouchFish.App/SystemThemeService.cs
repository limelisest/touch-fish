using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;

namespace TouchFish.App;

public sealed class SystemThemeService(System.Windows.Application application) : IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private bool _started;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        ApplyCurrentTheme();
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        application.Dispatcher.BeginInvoke(ApplyCurrentTheme);
    }

    private void ApplyCurrentTheme()
    {
        var isDark = IsDarkAppMode();
        application.ThemeMode = isDark ? ThemeMode.Dark : ThemeMode.Light;

        var colors = isDark ? ThemePalette.Dark : ThemePalette.Light;
        SetBrush("AppBackgroundBrush", colors.AppBackground);
        SetBrush("SurfaceBrush", colors.Surface);
        SetBrush("SurfaceAlternateBrush", colors.SurfaceAlternate);
        SetBrush("ControlBackgroundBrush", colors.ControlBackground);
        SetBrush("ControlHoverBrush", colors.ControlHover);
        SetBrush("BorderBrush", colors.Border);
        SetBrush("TextPrimaryBrush", colors.TextPrimary);
        SetBrush("TextSecondaryBrush", colors.TextSecondary);
        SetBrush("SelectionBrush", colors.Selection);
        SetBrush("AccentBrush", colors.Accent);
    }

    private static bool IsDarkAppMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    private void SetBrush(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        application.Resources[key] = brush;
    }

    public void Dispose()
    {
        if (!_started)
        {
            return;
        }

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _started = false;
    }

    private sealed record ThemePalette(
        Color AppBackground,
        Color Surface,
        Color SurfaceAlternate,
        Color ControlBackground,
        Color ControlHover,
        Color Border,
        Color TextPrimary,
        Color TextSecondary,
        Color Selection,
        Color Accent)
    {
        public static ThemePalette Light { get; } = new(
            Color.FromRgb(0xF3, 0xF5, 0xF8),
            Colors.White,
            Color.FromRgb(0xF8, 0xFA, 0xFC),
            Colors.White,
            Color.FromRgb(0xE9, 0xED, 0xF3),
            Color.FromRgb(0xC8, 0xD0, 0xDB),
            Color.FromRgb(0x20, 0x24, 0x2B),
            Color.FromRgb(0x66, 0x70, 0x85),
            Color.FromRgb(0xD8, 0xE9, 0xFF),
            Color.FromRgb(0x3B, 0x73, 0xC5));

        public static ThemePalette Dark { get; } = new(
            Color.FromRgb(0x14, 0x16, 0x19),
            Color.FromRgb(0x1D, 0x20, 0x24),
            Color.FromRgb(0x24, 0x28, 0x2D),
            Color.FromRgb(0x27, 0x2B, 0x30),
            Color.FromRgb(0x32, 0x37, 0x3D),
            Color.FromRgb(0x46, 0x4D, 0x56),
            Color.FromRgb(0xF1, 0xF3, 0xF5),
            Color.FromRgb(0xA8, 0xB0, 0xBC),
            Color.FromRgb(0x21, 0x4F, 0x78),
            Color.FromRgb(0x78, 0xA9, 0xF0));
    }
}
