using System.Text.RegularExpressions;

namespace TouchFish.Modules.Reader;

public sealed partial class ReaderChapterParser
{
    public IReadOnlyList<ReaderChapter> Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [new ReaderChapter { Title = "全文", StartIndex = 0, Length = 0 }];
        }

        var matches = ChapterHeadingRegex().Matches(text)
            .Cast<Match>()
            .Where(match => match.Success)
            .ToArray();
        if (matches.Length == 0)
        {
            return [new ReaderChapter { Title = "全文", StartIndex = 0, Length = text.Length }];
        }

        var chapters = new List<ReaderChapter>();
        if (matches[0].Index > 0 && !string.IsNullOrWhiteSpace(text[..matches[0].Index]))
        {
            chapters.Add(new ReaderChapter
            {
                Title = "序章",
                StartIndex = 0,
                Length = matches[0].Index
            });
        }

        for (var index = 0; index < matches.Length; index++)
        {
            var match = matches[index];
            var end = index + 1 < matches.Length ? matches[index + 1].Index : text.Length;
            chapters.Add(new ReaderChapter
            {
                Title = match.Groups["title"].Value.Trim(),
                StartIndex = match.Index,
                Length = end - match.Index
            });
        }

        return chapters;
    }

    [GeneratedRegex(
        @"(?im)^[ \t]*(?<title>(?:第[0-9零一二三四五六七八九十百千万两〇○]+[章回节卷部篇集][^\r\n]{0,40}|(?:chapter|part)\s+\d+[^\r\n]{0,40}))[ \t]*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ChapterHeadingRegex();
}
