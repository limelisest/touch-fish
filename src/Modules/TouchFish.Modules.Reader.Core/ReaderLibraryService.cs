using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TouchFish.Modules.Reader;

public sealed class ReaderLibraryService(ReaderChapterParser chapterParser)
{
    private const string MetadataFileName = "metadata.json";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _libraryRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "LimeLisest",
        "TouchFish",
        "books");

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
            Chapters = chapterParser.Parse(text).ToList()
        };
        var directory = GetBookDirectory(book.Id);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, book.FileName), text, new UTF8Encoding(false), cancellationToken);
        await SaveAsync(book, cancellationToken);
        return book;
    }

    public async Task<string> ReadChapterAsync(ReaderBook book, int chapterIndex, CancellationToken cancellationToken = default)
    {
        var text = await File.ReadAllTextAsync(GetBookTextPath(book), Encoding.UTF8, cancellationToken);
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
        var directory = GetBookDirectory(book.Id);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        return Task.CompletedTask;
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
