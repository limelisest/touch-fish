using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TouchFish.Modules.Reader;

public sealed class ReaderLibraryService
{
    private const string MetadataFileName = "metadata.json";
    private readonly ReaderChapterParser _chapterParser;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, Task<string>> _textCache = new();
    private readonly string _libraryRoot;

    public ReaderLibraryService(ReaderChapterParser chapterParser)
        : this(
            chapterParser,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "LimeLisest",
                "TouchFish",
                "books"))
    {
    }

    public ReaderLibraryService(ReaderChapterParser chapterParser, string libraryRoot)
    {
        _chapterParser = chapterParser;
        _libraryRoot = libraryRoot;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public string LibraryRoot => _libraryRoot;

    public async Task<IReadOnlyList<ReaderBook>> LoadBooksAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_libraryRoot);
        CleanupIncompleteImports();
        var books = new List<ReaderBook>();
        foreach (var metadataPath in Directory.EnumerateFiles(_libraryRoot, MetadataFileName, SearchOption.AllDirectories))
        {
            try
            {
                await using var stream = File.OpenRead(metadataPath);
                var book = await JsonSerializer.DeserializeAsync<ReaderBook>(stream, JsonOptions, cancellationToken);
                if (book is not null && File.Exists(GetBookTextPath(book)))
                {
                    books.Add(book);
                }
            }
            catch
            {
                // Ignore one damaged book entry and keep loading the remaining library.
            }
        }

        return books.OrderBy(book => book.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public async Task<ReaderBook> ImportAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken);
        var (encoding, text) = Decode(bytes);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        var book = new ReaderBook
        {
            Id = Guid.NewGuid(),
            Title = Path.GetFileNameWithoutExtension(sourcePath),
            ImportedEncoding = encoding.WebName,
            Chapters = _chapterParser.Parse(text).ToList()
        };
        var directory = GetBookDirectory(book.Id);
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, book.FileName), text, new UTF8Encoding(false), cancellationToken);
            _textCache[book.Id] = Task.FromResult(text);
            await SaveAsync(book, cancellationToken);
            return book;
        }
        catch
        {
            _textCache.TryRemove(book.Id, out _);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            throw;
        }
    }

    public async Task WarmBookAsync(ReaderBook book)
    {
        foreach (var cachedBookId in _textCache.Keys.Where(id => id != book.Id))
        {
            _textCache.TryRemove(cachedBookId, out _);
        }

        _ = await GetBookTextAsync(book);
    }

    public async Task<string> ReadChapterAsync(ReaderBook book, int chapterIndex, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = await GetBookTextAsync(book);
        if (book.Chapters.Count == 0)
        {
            return text;
        }

        var safeIndex = Math.Clamp(chapterIndex, 0, book.Chapters.Count - 1);
        var chapter = book.Chapters[safeIndex];
        var start = Math.Clamp(chapter.StartIndex, 0, text.Length);
        var length = Math.Clamp(chapter.Length, 0, text.Length - start);
        return text.Substring(start, length);
    }

    public async Task SaveAsync(ReaderBook book, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = GetBookDirectory(book.Id);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, MetadataFileName);
            var temporaryPath = $"{path}.tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, book, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, path, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task DeleteAsync(ReaderBook book)
    {
        _textCache.TryRemove(book.Id, out _);
        var directory = GetBookDirectory(book.Id);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        return Task.CompletedTask;
    }

    private void CleanupIncompleteImports()
    {
        foreach (var directory in Directory.EnumerateDirectories(_libraryRoot))
        {
            var name = Path.GetFileName(directory);
            if (Guid.TryParseExact(name, "N", out _) && !File.Exists(Path.Combine(directory, MetadataFileName)))
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch
                {
                    // A locked incomplete import can be retried on the next launch.
                }
            }
        }
    }

    private async Task<string> GetBookTextAsync(ReaderBook book)
    {
        var task = _textCache.GetOrAdd(
            book.Id,
            _ => File.ReadAllTextAsync(GetBookTextPath(book), Encoding.UTF8));
        try
        {
            return await task;
        }
        catch
        {
            if (_textCache.TryGetValue(book.Id, out var cachedTask) && ReferenceEquals(cachedTask, task))
            {
                _textCache.TryRemove(book.Id, out _);
            }

            throw;
        }
    }

    private string GetBookDirectory(Guid id) => Path.Combine(_libraryRoot, id.ToString("N"));

    private string GetBookTextPath(ReaderBook book) => Path.Combine(GetBookDirectory(book.Id), book.FileName);

    private static (Encoding Encoding, string Text) Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (Encoding.UTF8, Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (Encoding.Unicode, Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
        }

        try
        {
            var utf8 = new UTF8Encoding(false, true);
            return (utf8, utf8.GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            var gb18030 = Encoding.GetEncoding("GB18030");
            return (gb18030, gb18030.GetString(bytes));
        }
    }
}
