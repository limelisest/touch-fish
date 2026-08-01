using System.Text;
using TouchFish.Modules.Reader;
using Xunit;

namespace TouchFish.Modules.BossKey.Tests;

public sealed class ReaderChapterParserTests
{
    private readonly ReaderChapterParser _parser = new();

    [Fact]
    public void Parse_ChineseHeadings_CreatesIntroductionAndChapters()
    {
        const string text = "作品简介\n这是简介。\n第一章 开始\n正文一\n第二章 继续\n正文二";

        var chapters = _parser.Parse(text);

        Assert.Equal(3, chapters.Count);
        Assert.Equal("序章", chapters[0].Title);
        Assert.Equal("第一章 开始", chapters[1].Title);
        Assert.Equal("第二章 继续", chapters[2].Title);
        Assert.Equal(text, string.Concat(chapters.Select(chapter =>
            text.Substring(chapter.StartIndex, chapter.Length))));
    }

    [Fact]
    public void Parse_EnglishHeadings_IsCaseInsensitive()
    {
        const string text = "CHAPTER 1 Start\nOne\nchapter 2 End\nTwo";

        var chapters = _parser.Parse(text);

        Assert.Equal(2, chapters.Count);
        Assert.Equal("CHAPTER 1 Start", chapters[0].Title);
        Assert.Equal("chapter 2 End", chapters[1].Title);
    }

    [Fact]
    public void Parse_WithoutHeadings_UsesWholeBook()
    {
        const string text = "没有章节标题的短篇小说。";

        var chapter = Assert.Single(_parser.Parse(text));

        Assert.Equal("全文", chapter.Title);
        Assert.Equal(0, chapter.StartIndex);
        Assert.Equal(text.Length, chapter.Length);
    }

    [Fact]
    public void LibraryRoot_IsUnderUserDocuments()
    {
        var library = new ReaderLibraryService(_parser);
        var expected = Path.Combine("LimeLisest", "TouchFish", "books");

        Assert.EndsWith(expected, library.LibraryRoot, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_Gb18030Book_PersistsMetadataWithoutNamedFloatingPointValues()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"TouchFishTests-{Guid.NewGuid():N}");
        var sourcePath = Path.Combine(temporaryRoot, "凡人修仙传.txt");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            const string text = "声明：测试文本\r\n第一章 初入江湖\r\n正文内容\r\n第二章 修炼\r\n更多正文";
            await File.WriteAllBytesAsync(sourcePath, Encoding.GetEncoding("GB18030").GetBytes(text));
            var libraryPath = Path.Combine(temporaryRoot, "books");
            var library = new ReaderLibraryService(_parser, libraryPath);

            var imported = await library.ImportAsync(sourcePath);
            imported.Title = "修改后的书名";
            imported.ReaderLineSpacing = 1.8;
            imported.ReaderParagraphSpacing = 12;
            await library.SaveAsync(imported);
            var loaded = Assert.Single(await library.LoadBooksAsync());
            var chapter = await library.ReadChapterAsync(loaded, 1);
            var metadata = await File.ReadAllTextAsync(Path.Combine(libraryPath, imported.Id.ToString("N"), "metadata.json"));

            Assert.Equal("修改后的书名", loaded.Title);
            Assert.Equal(1.8, loaded.ReaderLineSpacing);
            Assert.Equal(12, loaded.ReaderParagraphSpacing);
            Assert.Equal(3, imported.Chapters.Count);
            Assert.Contains("第一章 初入江湖", chapter);
            Assert.DoesNotContain("NaN", metadata, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Infinity", metadata, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }
    }
}
