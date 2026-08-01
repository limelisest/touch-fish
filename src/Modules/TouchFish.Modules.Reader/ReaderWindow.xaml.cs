using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TouchFish.UI.FloatingWidgets;

namespace TouchFish.Modules.Reader;

public partial class ReaderWindow : Window
{
    private const int WindowHitTestMessage = 0x0084;
    private readonly ReaderLibraryService _library;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _pointerTimer;
    private ReaderBook? _book;
    private Guid? _loadedBookId;
    private int _loadedChapterIndex = -1;
    private HwndSource? _windowSource;
    private bool _allowClose;
    private bool _isClosed;
    private bool _loading;
    private bool _pointerSeenInside;

    public ReaderWindow(ReaderLibraryService library)
    {
        _library = library;
        InitializeComponent();
        Closing += OnClosing;
        SourceInitialized += OnSourceInitialized;
        Closed += (_, _) =>
        {
            _isClosed = true;
            _windowSource?.RemoveHook(WindowMessageHook);
        };
        LocationChanged += (_, _) => ScheduleSave();
        SizeChanged += (_, _) => ScheduleSave();
        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        ReaderScroll.ScrollChanged += (_, _) => ScheduleSave();
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _saveTimer.Tick += async (_, _) =>
        {
            _saveTimer.Stop();
            await SaveStateAsync();
        };
        _pointerTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _pointerTimer.Tick += OnPointerTimerTick;
    }

    public ReaderBook? CurrentBook => _book;
    public event Action<ReaderBook, int>? ChapterChanged;
    public event Action<ReaderBook, double>? OpacityChanged;
    public event Action? PointerExited;
    public event Action? DismissRequested;

    public async Task ShowBookAsync(ReaderBook book, int chapterIndex)
    {
        if (_book is not null && _book.Id != book.Id)
        {
            await SaveStateAsync();
        }

        var safeIndex = book.Chapters.Count == 0 ? 0 : Math.Clamp(chapterIndex, 0, book.Chapters.Count - 1);
        var contentAlreadyLoaded = _loadedBookId == book.Id &&
                                   _loadedChapterIndex == safeIndex &&
                                   !string.IsNullOrEmpty(ReaderText.Text);
        _book = book;
        ApplyAppearance(book);
        Width = Math.Max(MinWidth, book.ReaderWindowWidth);
        Height = Math.Max(MinHeight, book.ReaderWindowHeight);
        Topmost = book.ReaderWindowTopmost;
        if (book.ReaderWindowLeft is { } left && book.ReaderWindowTop is { } top)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        if (!contentAlreadyLoaded)
        {
            ReaderText.Text = "正在加载……";
            ReaderScroll.ScrollToHome();
            await LoadChapterAsync(safeIndex, restorePosition: true);
        }
    }

    public async Task LoadChapterAsync(int chapterIndex, bool restorePosition = false)
    {
        if (_book is null || _book.Chapters.Count == 0 || _loading)
        {
            return;
        }

        _loading = true;
        _saveTimer.Stop();
        try
        {
            SaveReadingOffset();
            var safeIndex = Math.Clamp(chapterIndex, 0, _book.Chapters.Count - 1);
            _book.CurrentChapterIndex = safeIndex;
            ReaderText.Text = await _library.ReadChapterAsync(_book, safeIndex);
            _loadedBookId = _book.Id;
            _loadedChapterIndex = safeIndex;
            ReaderScroll.ScrollToHome();
            await Dispatcher.InvokeAsync(() =>
            {
                ReaderScroll.UpdateLayout();
                if (restorePosition)
                {
                    var progress = Math.Clamp(_book.CurrentScrollProgress, 0, 1);
                    if (progress <= 0 && _book.CurrentCharacterOffset > 0 && ReaderText.Text.Length > 0)
                    {
                        progress = Math.Clamp((double)_book.CurrentCharacterOffset / ReaderText.Text.Length, 0, 1);
                    }

                    ReaderScroll.ScrollToVerticalOffset(progress * ReaderScroll.ScrollableHeight);
                }
                else
                {
                    _book.CurrentCharacterOffset = 0;
                    _book.CurrentScrollProgress = 0;
                }
            }, DispatcherPriority.Loaded);

            ChapterChanged?.Invoke(_book, safeIndex);
            try
            {
                await _library.SaveAsync(_book);
            }
            catch
            {
                // Progress saving will retry on the next scroll or window change.
            }
        }
        finally
        {
            _loading = false;
        }
    }

    public void ApplyAppearance(ReaderBook book)
    {
        var fontSize = Math.Clamp(book.ReaderFontSize, 10, 48);
        try
        {
            ReaderText.FontFamily = new FontFamily(book.ReaderFontFamily);
        }
        catch
        {
            ReaderText.FontFamily = new FontFamily("Microsoft YaHei UI");
        }

        ReaderText.FontSize = fontSize;
        ReaderText.LineHeight = fontSize * 1.65;
        var opacity = Math.Clamp(book.ReaderWindowOpacity, 0.25, 1);
        Opacity = opacity;
        OpacitySlider.Value = opacity;
    }

    private void OpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var opacity = Math.Clamp(e.NewValue, 0.25, 1);
        Opacity = opacity;
        if (_book is not null)
        {
            _book.ReaderWindowOpacity = opacity;
            OpacityChanged?.Invoke(_book, opacity);
            ScheduleSave();
        }
    }

    private async void PreviousChapter_OnClick(object sender, RoutedEventArgs e)
    {
        if (_book is null)
        {
            return;
        }

        try
        {
            await LoadChapterAsync(_book.CurrentChapterIndex - 1);
        }
        catch
        {
            // Keep the reader available if one chapter cannot be loaded.
        }
    }

    private async void NextChapter_OnClick(object sender, RoutedEventArgs e)
    {
        if (_book is null)
        {
            return;
        }

        try
        {
            await LoadChapterAsync(_book.CurrentChapterIndex + 1);
        }
        catch
        {
            // Keep the reader available if one chapter cannot be loaded.
        }
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || IsInteractiveControl(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
            e.Handled = true;
        }
        catch (InvalidOperationException)
        {
            // The button was released before WPF entered the native move loop.
        }
    }

    private static bool IsInteractiveControl(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is ButtonBase or Slider or ScrollBar or Thumb)
            {
                return true;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return false;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        FloatingWindowStyles.HideFromAltTab(this);
        _windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _windowSource?.AddHook(WindowMessageHook);
    }

    private nint WindowMessageHook(nint windowHandle, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != WindowHitTestMessage ||
            !NativeMethods.GetCursorPos(out var point) ||
            !NativeMethods.GetWindowRect(windowHandle, out var rect))
        {
            return nint.Zero;
        }

        const int border = 8;
        var left = point.X < rect.Left + border;
        var right = point.X >= rect.Right - border;
        var top = point.Y < rect.Top + border;
        var bottom = point.Y >= rect.Bottom - border;
        var result = (left, right, top, bottom) switch
        {
            (true, _, true, _) => 13,  // HTTOPLEFT
            (_, true, true, _) => 14,  // HTTOPRIGHT
            (true, _, _, true) => 16,  // HTBOTTOMLEFT
            (_, true, _, true) => 17,  // HTBOTTOMRIGHT
            (true, _, _, _) => 10,     // HTLEFT
            (_, true, _, _) => 11,     // HTRIGHT
            (_, _, true, _) => 12,     // HTTOP
            (_, _, _, true) => 15,     // HTBOTTOM
            _ => 0
        };
        if (result != 0)
        {
            handled = true;
            return result;
        }

        return nint.Zero;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose || Application.Current?.Dispatcher.HasShutdownStarted == true)
        {
            return;
        }

        e.Cancel = true;
        DismissRequested?.Invoke();
        if (IsVisible)
        {
            _ = SaveStateAsync();
            Hide();
        }
    }

    public void StartPointerTracking()
    {
        _pointerSeenInside = false;
        _pointerTimer.Start();
    }

    public void StopPointerTracking() => _pointerTimer.Stop();

    public void HideForWidget()
    {
        _pointerTimer.Stop();
        _ = SaveStateAsync();
        Hide();
    }

    private void OnPointerTimerTick(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == nint.Zero || !NativeMethods.GetCursorPos(out var point) || !NativeMethods.GetWindowRect(handle, out var rect))
        {
            return;
        }

        const int tolerance = 4;
        var inside = point.X >= rect.Left - tolerance && point.X < rect.Right + tolerance &&
                     point.Y >= rect.Top - tolerance && point.Y < rect.Bottom + tolerance;
        if (inside)
        {
            _pointerSeenInside = true;
            return;
        }

        if (_pointerSeenInside)
        {
            _pointerTimer.Stop();
            PointerExited?.Invoke();
        }
    }

    private void ScheduleSave()
    {
        if (_book is null)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveReadingOffset()
    {
        if (_book is null || string.IsNullOrEmpty(ReaderText.Text) || ReaderText.Text == "正在加载……")
        {
            return;
        }

        var progress = ReaderScroll.ScrollableHeight > 0
            ? Math.Clamp(ReaderScroll.VerticalOffset / ReaderScroll.ScrollableHeight, 0, 1)
            : 0;
        _book.CurrentScrollProgress = progress;
        _book.CurrentCharacterOffset = (int)Math.Round(ReaderText.Text.Length * progress);
    }

    public async Task SaveStateAsync()
    {
        if (_book is null || _loading)
        {
            return;
        }

        SaveReadingOffset();
        if (WindowState == WindowState.Normal)
        {
            _book.ReaderWindowLeft = Left;
            _book.ReaderWindowTop = Top;
            _book.ReaderWindowWidth = ActualWidth;
            _book.ReaderWindowHeight = ActualHeight;
        }

        _book.ReaderWindowTopmost = Topmost;
        try
        {
            await _library.SaveAsync(_book);
        }
        catch
        {
            // Reading state is best-effort and will be retried on the next scroll or window change.
        }
    }

    public void PrepareForShutdown()
    {
        _allowClose = true;
        _saveTimer.Stop();
        _pointerTimer.Stop();
    }

    public void Shutdown()
    {
        PrepareForShutdown();
        if (!_isClosed)
        {
            Close();
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(nint windowHandle, out Rect rect);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
