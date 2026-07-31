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

    public ReaderWindowManager(ReaderLibraryService library, IToolWindowRegistry registry)
    {
        _library = library;
        _registry = registry;
        registry.Register(this);
    }

    public string Id => "reader.window";
    public bool IsAvailable => _activeBook is not null;
    public bool IsMinimizedOrHidden => _readerWindow is null || !_readerWindow.IsVisible || _readerWindow.WindowState == WindowState.Minimized;

    public void SetActiveBook(ReaderBookItemViewModel? book)
    {
        if (_activeBook?.Id != book?.Id)
        {
            _widget?.Close();
            _widget = null;
        }

        _activeBook = book;
        if (book is null && _readerWindow?.IsVisible == true)
        {
            _readerWindow.Hide();
        }

        SyncFloatingWidget();
    }

    public async Task OpenAsync(ReaderBookItemViewModel book, int chapterIndex)
    {
        _activeBook = book;
        book.ApplyToModel();
        _readerWindow ??= new ReaderWindow(_library);
        _readerWindow.Topmost = book.ReaderWindowTopmost;
        await _readerWindow.ShowBookAsync(book.Model, chapterIndex);
        SyncFloatingWidget();
    }

    public void SyncFloatingWidget()
    {
        if (_activeBook is null || !_activeBook.FloatingWidgetEnabled)
        {
            _widget?.Close();
            _widget = null;
            return;
        }

        var book = _activeBook;
        if (_widget is null)
        {
            _widget = new FloatingWidgetWindow();
            _widget.ActivationRequested += () =>
            {
                var activeBook = _activeBook;
                if (activeBook is not null)
                {
                    _ = OpenAsync(activeBook, activeBook.Model.CurrentChapterIndex);
                }
            };
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

        if (_readerWindow is not null && _readerWindow.CurrentBook?.Id == book.Id)
        {
            _readerWindow.Topmost = book.ReaderWindowTopmost;
        }

        _widget.TriggerMode = book.FloatingWidgetTriggerMode;
        _widget.EdgeSnapEnabled = book.FloatingWidgetEdgeSnapEnabled;
        _widget.UpdateContent(book.Title, null);
        _ = SaveBookAsync(book);
    }

    public bool Minimize()
    {
        if (_readerWindow is null || !_readerWindow.IsVisible)
        {
            return false;
        }

        _readerWindow.WindowState = WindowState.Minimized;
        return true;
    }

    public bool Restore()
    {
        if (_activeBook is null)
        {
            return false;
        }

        if (_readerWindow is not null && _readerWindow.CurrentBook?.Id == _activeBook.Id)
        {
            _readerWindow.Show();
            _readerWindow.WindowState = WindowState.Normal;
            _readerWindow.Activate();
            _readerWindow.Topmost = _activeBook.ReaderWindowTopmost;
            return true;
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

    public void Dispose()
    {
        _registry.Unregister(Id);
        _widget?.Close();
        _readerWindow?.Shutdown();
    }
}
