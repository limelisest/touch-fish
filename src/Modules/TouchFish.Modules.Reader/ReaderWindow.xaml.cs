using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TouchFish.Modules.Reader;

public partial class ReaderWindow : Window
{
    private readonly ReaderLibraryService _library;
    private readonly DispatcherTimer _saveTimer;
    private ReaderBook? _book;
    private bool _allowClose;
    private bool _loading;

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
    }

    public ReaderBook? CurrentBook => _book;

    public async Task ShowBookAsync(ReaderBook book, int chapterIndex)
    {
        if (_book is not null && _book.Id != book.Id)
        {
            await SaveStateAsync();
        }

        _book = book;
        ReaderText.Text = string.Empty;
        Width = Math.Max(MinWidth, book.ReaderWindowWidth);
        Height = Math.Max(MinHeight, book.ReaderWindowHeight);
        Topmost = book.ReaderWindowTopmost;
        if (!double.IsNaN(book.ReaderWindowLeft) && !double.IsNaN(book.ReaderWindowTop))
        {
            Left = book.ReaderWindowLeft;
            Top = book.ReaderWindowTop;
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
        try
        {
            SaveReadingOffset();
            var safeIndex = Math.Clamp(chapterIndex, 0, _book.Chapters.Count - 1);
            _book.CurrentChapterIndex = safeIndex;
            var chapter = _book.Chapters[safeIndex];
            ChapterTitle.Text = $"{_book.Title} · {chapter.Title}";
            Title = $"{_book.Title} - {chapter.Title}";
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

            await _library.SaveAsync(_book);
        }
        finally
        {
            _loading = false;
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
        _ = SaveStateAsync();
        Hide();
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
        if (line >= 0)
        {
            _book.CurrentCharacterOffset = Math.Max(0, ReaderText.GetCharacterIndexFromLineIndex(line));
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
        await _library.SaveAsync(_book);
    }

    public void Shutdown()
    {
        _allowClose = true;
        _saveTimer.Stop();
        Close();
    }
}
