using TouchFish.Contracts;

namespace TouchFish.Modules.Reader;

public sealed class ReaderBook
{
    public int SchemaVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = "book.txt";
    public string ImportedEncoding { get; set; } = "utf-8";
    public List<ReaderChapter> Chapters { get; set; } = [];
    public int CurrentChapterIndex { get; set; }
    public int CurrentCharacterOffset { get; set; }
    public double ReaderWindowLeft { get; set; } = double.NaN;
    public double ReaderWindowTop { get; set; } = double.NaN;
    public double ReaderWindowWidth { get; set; } = 560;
    public double ReaderWindowHeight { get; set; } = 420;
    public bool ReaderWindowTopmost { get; set; } = true;
    public bool FloatingWidgetEnabled { get; set; }
    public FloatingWidgetTriggerMode FloatingWidgetTriggerMode { get; set; } = FloatingWidgetTriggerMode.Click;
    public bool FloatingWidgetEdgeSnapEnabled { get; set; } = true;
    public double? FloatingWidgetLeft { get; set; }
    public double? FloatingWidgetTop { get; set; }
}

public sealed class ReaderChapter
{
    public string Title { get; set; } = string.Empty;
    public int StartIndex { get; set; }
    public int Length { get; set; }
}
