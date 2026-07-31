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
}
