using System.Windows;
using System.Windows.Threading;
using TouchFish.Contracts;
using TouchFish.UI.FloatingWidgets;

namespace TouchFish.Modules.Reader;

public sealed class ReaderWindowManager : IManagedToolWindow, IDisposable
{
    private readonly ReaderLibraryService _library;
    private readonly IToolWindowRegistry _registry;
    private ReaderWindow? _readerWindow;
    private FloatingWidgetWindow? _widget;
    private ReaderBookItemViewModel? _activeBook;
    private readonly DispatcherTimer _autoHideTimer;
    private DateTimeOffset? _entryGraceUntil;
    private DateTimeOffset? _cursorLeftAt;
    private bool _autoHideActive;
    private bool _isShuttingDown;
    private bool _featureEnabled = true;

    public ReaderWindowManager(ReaderLibraryService library, IToolWindowRegistry registry)
    {
        _library = library;
        _registry = registry;
        _autoHideTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _autoHideTimer.Tick += OnAutoHideTimerTick;
        registry.Register(this);
    }

    public string Id => "reader.window";
    public event Action<ReaderBook, int>? ChapterChanged;
    public event Action<ReaderBook, double>? OpacityChanged;
    public bool FeatureEnabled => _featureEnabled;
    public bool IsAvailable => _featureEnabled && _activeBook is not null;
    public bool IsMinimizedOrHidden => _readerWindow is null || !_readerWindow.IsVisible || _readerWindow.WindowState == WindowState.Minimized;

    public void SetActiveBook(ReaderBookItemViewModel? book)
    {
        if (_isShuttingDown)
        {
            return;
        }

        if (_activeBook?.Id != book?.Id)
        {
            StopAutoHide();
            _widget?.Close();
            _widget = null;
            if (_readerWindow?.IsVisible == true)
            {
                _ = _readerWindow.SaveStateAsync();
                _readerWindow.Hide();
            }
        }

        _activeBook = book;
        if (book is null && _readerWindow?.IsVisible == true)
        {
            StopAutoHide();
            _readerWindow.Hide();
        }

        SyncFloatingWidget();
    }

    public void SetFeatureEnabled(bool enabled)
    {
        _featureEnabled = enabled;
        if (!enabled)
        {
            StopAutoHide();
            _widget?.Close();
            _widget = null;
            if (_readerWindow?.IsVisible == true)
            {
                _ = _readerWindow.SaveStateAsync();
                _readerWindow.Hide();
            }

            return;
        }

        SyncFloatingWidget();
    }

    public Task OpenAsync(ReaderBookItemViewModel book, int chapterIndex) =>
        OpenAsync(book, chapterIndex, fromWidget: false);

    private async Task OpenAsync(ReaderBookItemViewModel book, int chapterIndex, bool fromWidget)
    {
        if (_isShuttingDown || !_featureEnabled)
        {
            return;
        }

        _activeBook = book;
        book.ApplyToModel();
        var window = EnsureReaderWindow();
        window.Topmost = book.ReaderWindowTopmost;
        await window.ShowBookAsync(book.Model, chapterIndex);
        SyncFloatingWidget();
        if (fromWidget)
        {
            StartAutoHide();
        }
        else
        {
            StopAutoHide();
        }
    }

    public void SyncFloatingWidget()
    {
        if (_isShuttingDown)
        {
            return;
        }

        if (!_featureEnabled)
        {
            StopAutoHide();
            _widget?.Close();
            _widget = null;
            return;
        }

        if (_activeBook is null)
        {
            _widget?.Close();
            _widget = null;
            return;
        }

        var book = _activeBook;
        if (_readerWindow is not null && _readerWindow.CurrentBook?.Id == book.Id)
        {
            _readerWindow.Topmost = book.ReaderWindowTopmost;
            _readerWindow.ApplyAppearance(book.Model);
        }

        if (!book.FloatingWidgetEnabled)
        {
            StopAutoHide();
            _widget?.Close();
            _widget = null;
            return;
        }

        if (_widget is null)
        {
            _widget = new FloatingWidgetWindow();
            _widget.ActivationRequested += OnWidgetActivationRequested;
            _widget.PositionChanged += (left, top) =>
            {
                var activeBook = _activeBook;
                if (activeBook is null)
                {
                    return;
                }

                activeBook.Model.FloatingWidgetLeft = left;
                activeBook.Model.FloatingWidgetTop = top;
                _ = SaveBookAsync(activeBook);
            };
            var workArea = SystemParameters.WorkArea;
            _widget.SetInitialPosition(
                book.Model.FloatingWidgetLeft ?? workArea.Right - 132,
                book.Model.FloatingWidgetTop ?? workArea.Top + 72);
            _widget.Show();
        }

        _widget.TriggerMode = book.FloatingWidgetTriggerMode;
        _widget.EdgeSnapEnabled = book.FloatingWidgetEdgeSnapEnabled;
        _widget.UpdateContent("看书", null);
        if (!_widget.IsVisible)
        {
            _widget.Show();
        }

        _ = SaveBookAsync(book);
    }

    private void OnWidgetActivationRequested()
    {
        var book = _activeBook;
        if (book is not null)
        {
            _ = OpenFromWidgetAsync(book);
        }
    }

    private async Task OpenFromWidgetAsync(ReaderBookItemViewModel book)
    {
        try
        {
            await OpenAsync(book, book.Model.CurrentChapterIndex, fromWidget: true);
        }
        catch
        {
            // The main reader page can retry and surface the file access error.
        }
    }

    private ReaderWindow EnsureReaderWindow()
    {
        if (_readerWindow is not null)
        {
            return _readerWindow;
        }

        _readerWindow = new ReaderWindow(_library);
        _readerWindow.ChapterChanged += (book, chapterIndex) => ChapterChanged?.Invoke(book, chapterIndex);
        _readerWindow.OpacityChanged += (book, opacity) => OpacityChanged?.Invoke(book, opacity);
        return _readerWindow;
    }

    private void StartAutoHide()
    {
        _autoHideActive = true;
        _entryGraceUntil = FloatingWidgetActivationPolicy.StartEntryGrace(DateTimeOffset.UtcNow);
        _cursorLeftAt = null;
        _autoHideTimer.Start();
    }

    private void StopAutoHide()
    {
        _autoHideActive = false;
        _entryGraceUntil = null;
        _cursorLeftAt = null;
        _autoHideTimer.Stop();
    }

    private void OnAutoHideTimerTick(object? sender, EventArgs e)
    {
        if (!_autoHideActive || _isShuttingDown || _readerWindow?.IsVisible != true || _activeBook is null)
        {
            StopAutoHide();
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (_readerWindow.IsCursorInside())
        {
            _entryGraceUntil = null;
            _cursorLeftAt = null;
            return;
        }

        if (_entryGraceUntil is not null &&
            FloatingWidgetActivationPolicy.IsEntryGraceActive(_entryGraceUntil.Value, now))
        {
            return;
        }

        _cursorLeftAt ??= now;
        var seconds = Math.Clamp(_activeBook.ReaderAutoHideSeconds, 0, 86400);
        if (!ReaderAutoHidePolicy.ShouldHide(_entryGraceUntil, _cursorLeftAt, seconds, now))
        {
            return;
        }

        StopAutoHide();
        _ = _readerWindow.SaveStateAsync();
        _readerWindow.Hide();
    }

    public bool Minimize()
    {
        if (_readerWindow is null || !_readerWindow.IsVisible)
        {
            return false;
        }

        StopAutoHide();
        _readerWindow.WindowState = WindowState.Minimized;
        return true;
    }

    public bool Restore()
    {
        if (_activeBook is null)
        {
            return false;
        }

        _ = OpenAsync(_activeBook, _activeBook.Model.CurrentChapterIndex);
        return true;
    }

    private async Task SaveBookAsync(ReaderBookItemViewModel book)
    {
        try
        {
            book.ApplyToModel();
            await _library.SaveAsync(book.Model);
        }
        catch
        {
            // An explicit save/open operation will retry.
        }
    }

    public void PrepareForShutdown()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        StopAutoHide();
        _readerWindow?.PrepareForShutdown();
        var widget = _widget;
        _widget = null;
        try
        {
            widget?.Close();
        }
        catch (InvalidOperationException)
        {
            // The application shutdown sequence may have already destroyed the native window.
        }
    }

    public void Dispose()
    {
        PrepareForShutdown();
        _registry.Unregister(Id);
        _readerWindow?.Shutdown();
    }
}
