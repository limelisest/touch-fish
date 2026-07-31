using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace TouchFish.Modules.Reader;

public partial class ReaderViewModel(
    ReaderLibraryService library,
    ReaderWindowManager windowManager) : ObservableObject, IDisposable
{
    private bool _restoringChapterSelection;

    public ObservableCollection<ReaderBookItemViewModel> Books { get; } = [];
    public ObservableCollection<ReaderChapter> Chapters { get; } = [];
    public string LibraryPath => library.LibraryRoot;

    [ObservableProperty] private ReaderBookItemViewModel? _selectedBook;
    [ObservableProperty] private ReaderChapter? _selectedChapter;
    [ObservableProperty] private string _statusText = "正在载入书库……";

    public async Task InitializeAsync()
    {
        Books.Clear();
        foreach (var book in await library.LoadBooksAsync())
        {
            var item = new ReaderBookItemViewModel(book);
            Attach(item);
            Books.Add(item);
        }

        SelectedBook = Books.FirstOrDefault();
        StatusText = Books.Count == 0
            ? "书库为空，请导入 TXT 小说。"
            : $"已载入 {Books.Count} 本书。";
    }

    partial void OnSelectedBookChanged(ReaderBookItemViewModel? oldValue, ReaderBookItemViewModel? newValue)
    {
        Chapters.Clear();
        if (newValue is not null)
        {
            foreach (var chapter in newValue.Chapters)
            {
                Chapters.Add(chapter);
            }

            _restoringChapterSelection = true;
            SelectedChapter = Chapters.ElementAtOrDefault(
                Math.Clamp(newValue.Model.CurrentChapterIndex, 0, Math.Max(0, Chapters.Count - 1)));
            _restoringChapterSelection = false;
        }
        else
        {
            SelectedChapter = null;
        }

        windowManager.SetActiveBook(newValue);
    }

    partial void OnSelectedChapterChanged(ReaderChapter? value)
    {
        if (_restoringChapterSelection || SelectedBook is null || value is null)
        {
            return;
        }

        var index = Chapters.IndexOf(value);
        if (index >= 0)
        {
            SelectedBook.Model.CurrentChapterIndex = index;
            SelectedBook.Model.CurrentCharacterOffset = 0;
            _ = SaveBookAsync(SelectedBook);
        }
    }

    [RelayCommand]
    private async Task ImportBookAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入 TXT 小说",
            Filter = "TXT 小说 (*.txt)|*.txt",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            StatusText = "正在导入并解析章节……";
            var model = await library.ImportAsync(dialog.FileName);
            var item = new ReaderBookItemViewModel(model);
            Attach(item);
            Books.Add(item);
            SelectedBook = item;
            StatusText = $"已导入《{item.Title}》，识别到 {item.Chapters.Count} 个章节。";
        }
        catch (UnauthorizedAccessException)
        {
            StatusText = "导入失败：没有读取源文件或写入文档书库的权限。";
        }
        catch (IOException exception)
        {
            StatusText = $"导入失败：文件正在使用或磁盘写入失败。{exception.Message}";
        }
        catch (Exception exception)
        {
            StatusText = $"导入失败：无法解析或保存该 TXT 文件。{exception.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenReaderAsync()
    {
        if (SelectedBook is null)
        {
            StatusText = "请先选择一本书。";
            return;
        }

        var index = SelectedChapter is null ? SelectedBook.Model.CurrentChapterIndex : Chapters.IndexOf(SelectedChapter);
        await windowManager.OpenAsync(SelectedBook, Math.Max(0, index));
        StatusText = $"正在阅读《{SelectedBook.Title}》。";
    }

    [RelayCommand]
    private async Task DeleteBookAsync()
    {
        if (SelectedBook is null)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"确定从本地书库删除《{SelectedBook.Title}》吗？",
            "删除书籍",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        var book = SelectedBook;
        Detach(book);
        Books.Remove(book);
        windowManager.SetActiveBook(null);
        await library.DeleteAsync(book.Model);
        SelectedBook = Books.FirstOrDefault();
        StatusText = $"已删除《{book.Title}》。";
    }

    [RelayCommand]
    private async Task SaveBookSettingsAsync()
    {
        if (SelectedBook is null)
        {
            return;
        }

        SelectedBook.ApplyToModel();
        await library.SaveAsync(SelectedBook.Model);
        windowManager.SyncFloatingWidget();
        StatusText = "阅读设置已保存。";
    }

    [RelayCommand]
    private void OpenLibraryFolder()
    {
        Directory.CreateDirectory(library.LibraryRoot);
        Process.Start(new ProcessStartInfo("explorer.exe", library.LibraryRoot) { UseShellExecute = true });
    }

    private void Attach(ReaderBookItemViewModel book) => book.PropertyChanged += OnBookPropertyChanged;
    private void Detach(ReaderBookItemViewModel book) => book.PropertyChanged -= OnBookPropertyChanged;

    private void OnBookPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ReaderBookItemViewModel book || e.PropertyName is not (
            nameof(ReaderBookItemViewModel.FloatingWidgetEnabled) or
            nameof(ReaderBookItemViewModel.FloatingWidgetTriggerMode) or
            nameof(ReaderBookItemViewModel.FloatingWidgetEdgeSnapEnabled) or
            nameof(ReaderBookItemViewModel.ReaderWindowTopmost)))
        {
            return;
        }

        book.ApplyToModel();
        windowManager.SyncFloatingWidget();
        _ = SaveBookAsync(book);
    }

    private async Task SaveBookAsync(ReaderBookItemViewModel book)
    {
        try
        {
            book.ApplyToModel();
            await library.SaveAsync(book.Model);
        }
        catch (Exception exception)
        {
            StatusText = $"阅读进度保存失败：{exception.Message}";
        }
    }

    public void Dispose()
    {
        foreach (var book in Books)
        {
            Detach(book);
        }
    }
}
