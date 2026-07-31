using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace TouchFish.Modules.Reader;

public partial class ReaderWindow : Window
{
    private readonly ReaderLibraryService _library;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _pointerTimer;
    private ReaderBook? _book;
    private bool _allowClose;
    private bool _loading;
    private bool _pointerSeenInside;

    public ReaderWindow(ReaderLibraryService library)
    {
        _library = library;
        InitializeComponent();
        Closing += OnClosing;
        LocationChanged += (_, _) => ScheduleSave();
        SizeChanged += (_, _) => ScheduleSave();
        ReaderText.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => ScheduleSave()));
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

        _book = book;
        ReaderText.Text = string.Empty;
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

        await LoadChapterAsync(chapterIndex, restorePosition: true);
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
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
            var chapter = _book.Chapters[safeIndex];
            ReaderText.Text = await _library.ReadChapterAsync(_book, safeIndex);
            ReaderText.ScrollToHome();
            if (restorePosition && _book.CurrentCharacterOffset > 0)
            {
                var offset = Math.Clamp(_book.CurrentCharacterOffset, 0, ReaderText.Text.Length);
                var line = ReaderText.GetLineIndexFromCharacterIndex(offset);
                if (line >= 0)
                {
                    ReaderText.ScrollToLine(line);
                }
            }
            else
            {
                _book.CurrentCharacterOffset = 0;
            }

            ChapterChanged?.Invoke(_book, safeIndex);
            await _library.SaveAsync(_book);
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
        TextBlock.SetLineHeight(ReaderText, fontSize * 1.65);
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
        if (_book is not null)
        {
            await LoadChapterAsync(_book.CurrentChapterIndex - 1);
        }
    }

    private async void NextChapter_OnClick(object sender, RoutedEventArgs e)
    {
        if (_book is not null)
        {
            await LoadChapterAsync(_book.CurrentChapterIndex + 1);
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
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
        if (_book is null || string.IsNullOrEmpty(ReaderText.Text))
        {
            return;
        }

        var line = ReaderText.GetFirstVisibleLineIndex();
        var lineCount = ReaderText.LineCount;
        if (line >= 0 && line < lineCount)
        {
            try
            {
                _book.CurrentCharacterOffset = Math.Max(0, ReaderText.GetCharacterIndexFromLineIndex(line));
            }
            catch (ArgumentOutOfRangeException)
            {
                // Text layout can still report a stale first-visible line while a chapter is changing.
            }
        }
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

    public void Shutdown()
    {
        _allowClose = true;
        _saveTimer.Stop();
        _pointerTimer.Stop();
        Close();
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
