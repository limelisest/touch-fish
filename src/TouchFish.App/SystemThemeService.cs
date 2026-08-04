using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
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
        ApplicationThemeManager.Apply(
            isDark ? ApplicationTheme.Dark : ApplicationTheme.Light,
            WindowBackdropType.Mica,
            updateAccent: false);

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
        SetBrush("AccentHoverBrush", colors.AccentHover);
        SetBrush("AccentForegroundBrush", colors.AccentForeground);
        SetBrush("DangerBrush", colors.Danger);
        SetBrush("DangerSurfaceBrush", colors.DangerSurface);
        SetBrush("SuccessBrush", colors.Success);
        SetBrush("SuccessSurfaceBrush", colors.SuccessSurface);
        SetBrush("StatusSurfaceBrush", colors.StatusSurface);
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
        Color Accent,
        Color AccentHover,
        Color AccentForeground,
        Color Danger,
        Color DangerSurface,
        Color Success,
        Color SuccessSurface,
        Color StatusSurface)
    {
        public static ThemePalette Light { get; } = new(
            Color.FromRgb(0xF5, 0xF7, 0xFB),
            Color.FromRgb(0xFC, 0xFD, 0xFF),
            Color.FromRgb(0xF0, 0xF3, 0xF9),
            Colors.White,
            Color.FromRgb(0xE8, 0xEC, 0xF5),
            Color.FromRgb(0xDD, 0xE2, 0xEC),
            Color.FromRgb(0x18, 0x20, 0x33),
            Color.FromRgb(0x68, 0x71, 0x87),
            Color.FromRgb(0xE4, 0xE9, 0xFF),
            Color.FromRgb(0x4F, 0x6B, 0xED),
            Color.FromRgb(0x40, 0x59, 0xD0),
            Colors.White,
            Color.FromRgb(0xD9, 0x2D, 0x4E),
            Color.FromRgb(0xFD, 0xEC, 0xEF),
            Color.FromRgb(0x16, 0x86, 0x5C),
            Color.FromRgb(0xE7, 0xF7, 0xF0),
            Color.FromRgb(0xEE, 0xF2, 0xFF));

        public static ThemePalette Dark { get; } = new(
            Color.FromRgb(0x11, 0x13, 0x18),
            Color.FromRgb(0x1A, 0x1D, 0x24),
            Color.FromRgb(0x22, 0x26, 0x30),
            Color.FromRgb(0x24, 0x28, 0x33),
            Color.FromRgb(0x2D, 0x33, 0x40),
            Color.FromRgb(0x34, 0x3A, 0x47),
            Color.FromRgb(0xF3, 0xF5, 0xFA),
            Color.FromRgb(0xAA, 0xB2, 0xC3),
            Color.FromRgb(0x2A, 0x34, 0x5C),
            Color.FromRgb(0x81, 0x94, 0xFF),
            Color.FromRgb(0x93, 0xA4, 0xFF),
            Color.FromRgb(0x11, 0x13, 0x18),
            Color.FromRgb(0xFF, 0x8E, 0xA3),
            Color.FromRgb(0x48, 0x23, 0x2D),
            Color.FromRgb(0x65, 0xD6, 0xA7),
            Color.FromRgb(0x19, 0x38, 0x2D),
            Color.FromRgb(0x22, 0x2B, 0x4B));
    }
}
