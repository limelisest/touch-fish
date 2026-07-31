using System.Windows;
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
    private bool _widgetArmed = true;
    private bool _collapsing;
    private bool _openingFromWidget;

    public ReaderWindowManager(ReaderLibraryService library, IToolWindowRegistry registry)
    {
        _library = library;
        _registry = registry;
        registry.Register(this);
    }

    public string Id => "reader.window";
    public event Action<ReaderBook, int>? ChapterChanged;
    public event Action<ReaderBook, double>? OpacityChanged;
    public bool IsAvailable => _activeBook is not null;
    public bool IsMinimizedOrHidden => _readerWindow is null || !_readerWindow.IsVisible || _readerWindow.WindowState == WindowState.Minimized;

    public void SetActiveBook(ReaderBookItemViewModel? book)
    {
        if (_activeBook?.Id != book?.Id)
        {
            _widget?.Close();
            _widget = null;
            if (_readerWindow?.IsVisible == true)
            {
                _readerWindow.StopPointerTracking();
                _ = _readerWindow.SaveStateAsync();
                _readerWindow.Hide();
            }
        }

        _activeBook = book;
        if (book is null && _readerWindow?.IsVisible == true)
        {
            _readerWindow.StopPointerTracking();
            _readerWindow.Hide();
        }

        SyncFloatingWidget();
    }

    public Task OpenAsync(ReaderBookItemViewModel book, int chapterIndex) =>
        OpenAsync(book, chapterIndex, fromWidget: false);

    private async Task OpenAsync(ReaderBookItemViewModel book, int chapterIndex, bool fromWidget)
    {
        _activeBook = book;
        book.ApplyToModel();
        var window = EnsureReaderWindow();
        if (book.FloatingWidgetEnabled && _widget is not null)
        {
            if (fromWidget)
            {
                var widgetWidth = _widget.ActualWidth > 0 ? _widget.ActualWidth : 120;
                var widgetHeight = _widget.ActualHeight > 0 ? _widget.ActualHeight : 40;
                (book.Model.ReaderWindowLeft, book.Model.ReaderWindowTop) = ReaderWidgetPlacement.CenterWindowOnWidget(
                    _widget.Left,
                    _widget.Top,
                    widgetWidth,
                    widgetHeight,
                    book.Model.ReaderWindowWidth,
                    book.Model.ReaderWindowHeight);
            }

            _widget.Hide();
        }

        window.Topmost = book.ReaderWindowTopmost;
        await window.ShowBookAsync(book.Model, chapterIndex);
        if (book.FloatingWidgetEnabled)
        {
            window.StartPointerTracking();
        }

        SyncFloatingWidget();
    }

    public void SyncFloatingWidget()
    {
        if (_activeBook is null)
        {
            _readerWindow?.StopPointerTracking();
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
            _readerWindow?.StopPointerTracking();
            _widget?.Close();
            _widget = null;
            return;
        }

        var widgetCreated = false;
        if (_widget is null)
        {
            widgetCreated = true;
            _widgetArmed = true;
            _widget = new FloatingWidgetWindow();
            _widget.PointerEntered += OnWidgetPointerEntered;
            _widget.PointerExited += () => _widgetArmed = true;
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
            var centeredWidgetPosition = book.Model.ReaderWindowLeft is { } readerLeft &&
                                         book.Model.ReaderWindowTop is { } readerTop
                ? ReaderWidgetPlacement.CenterWidgetOnWindow(
                    readerLeft,
                    readerTop,
                    book.Model.ReaderWindowWidth,
                    book.Model.ReaderWindowHeight,
                    120,
                    40)
                : ((double Left, double Top)?)null;
            var left = centeredWidgetPosition?.Left ?? book.Model.FloatingWidgetLeft ?? workArea.Right - 132;
            var top = centeredWidgetPosition?.Top ?? book.Model.FloatingWidgetTop ?? workArea.Top + 72;
            _widget.SetInitialPosition(left, top);
            if (_readerWindow?.IsVisible != true)
            {
                _widget.Show();
            }
        }

        if (_readerWindow is not null && _readerWindow.CurrentBook?.Id == book.Id)
        {
            if (_readerWindow.IsVisible)
            {
                if (widgetCreated)
                {
                    CollapseToWidget();
                    return;
                }

                _widget.Hide();
                _readerWindow.StartPointerTracking();
            }
            else if (!_widget.IsVisible && !_collapsing)
            {
                _widgetArmed = true;
                _widget.Show();
            }
        }

        _widget.EdgeSnapEnabled = book.FloatingWidgetEdgeSnapEnabled;
        _widget.UpdateContent(book.Title, null);
        _ = SaveBookAsync(book);
    }

    private void OnWidgetPointerEntered()
    {
        var book = _activeBook;
        if (!_widgetArmed || _openingFromWidget || book is null || _widget is null || !_widget.IsVisible)
        {
            return;
        }

        _widgetArmed = false;
        _ = OpenFromWidgetAsync(book);
    }

    private async Task OpenFromWidgetAsync(ReaderBookItemViewModel book)
    {
        _openingFromWidget = true;
        try
        {
            await OpenAsync(book, book.Model.CurrentChapterIndex, fromWidget: true);
        }
        catch
        {
            if (_widget is not null)
            {
                _widgetArmed = true;
                _widget.Show();
            }
        }
        finally
        {
            _openingFromWidget = false;
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
        _readerWindow.PointerExited += CollapseToWidget;
        _readerWindow.DismissRequested += CollapseToWidget;
        return _readerWindow;
    }

    private void CollapseToWidget()
    {
        if (_collapsing || _activeBook is null || _readerWindow is null || !_activeBook.FloatingWidgetEnabled)
        {
            return;
        }

        _collapsing = true;
        try
        {
            var book = _activeBook;
            var bounds = _readerWindow.WindowState == WindowState.Normal
                ? new Rect(_readerWindow.Left, _readerWindow.Top, _readerWindow.ActualWidth, _readerWindow.ActualHeight)
                : _readerWindow.RestoreBounds;
            book.Model.ReaderWindowLeft = bounds.Left;
            book.Model.ReaderWindowTop = bounds.Top;
            var widgetWidth = _widget is { ActualWidth: > 0 } currentWidget ? currentWidget.ActualWidth : 120;
            var widgetHeight = _widget is { ActualHeight: > 0 } measuredWidget ? measuredWidget.ActualHeight : 40;
            (book.Model.FloatingWidgetLeft, book.Model.FloatingWidgetTop) = ReaderWidgetPlacement.CenterWidgetOnWindow(
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                widgetWidth,
                widgetHeight);
            _readerWindow.HideForWidget();
            SyncFloatingWidget();
            if (_widget is not null)
            {
                _widgetArmed = false;
                _widget.SetInitialPosition(
                    book.Model.FloatingWidgetLeft.Value,
                    book.Model.FloatingWidgetTop.Value);
                _widget.Show();
                _ = RearmWidgetAsync(_widget);
            }

            _ = SaveBookAsync(book);
        }
        finally
        {
            _collapsing = false;
        }
    }

    private async Task RearmWidgetAsync(FloatingWidgetWindow widget)
    {
        await Task.Delay(250);
        if (ReferenceEquals(widget, _widget) && !widget.IsMouseOver)
        {
            _widgetArmed = true;
        }
    }

    public bool Minimize()
    {
        if (_readerWindow is null || !_readerWindow.IsVisible)
        {
            return false;
        }

        if (_activeBook?.FloatingWidgetEnabled == true)
        {
            CollapseToWidget();
        }
        else
        {
            _readerWindow.WindowState = WindowState.Minimized;
        }

        return true;
    }

    public bool Restore()
    {
        if (_activeBook is null)
        {
            return false;
        }

        _ = OpenAsync(_activeBook, _activeBook.Model.CurrentChapterIndex, fromWidget: false);
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

    public void Dispose()
    {
        _registry.Unregister(Id);
        _widget?.Close();
        _readerWindow?.Shutdown();
    }
}
