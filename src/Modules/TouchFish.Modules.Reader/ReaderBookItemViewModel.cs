using CommunityToolkit.Mvvm.ComponentModel;
using TouchFish.Contracts;

namespace TouchFish.Modules.Reader;

public partial class ReaderBookItemViewModel(ReaderBook model) : ObservableObject
{
    public ReaderBook Model { get; } = model;
    public Guid Id => Model.Id;
    public string Title => Model.Title;
    public IReadOnlyList<ReaderChapter> Chapters => Model.Chapters;

    [ObservableProperty] private bool _floatingWidgetEnabled = model.FloatingWidgetEnabled;
    [ObservableProperty] private FloatingWidgetTriggerMode _floatingWidgetTriggerMode = model.FloatingWidgetTriggerMode;
    [ObservableProperty] private bool _floatingWidgetEdgeSnapEnabled = model.FloatingWidgetEdgeSnapEnabled;
    [ObservableProperty] private bool _readerWindowTopmost = model.ReaderWindowTopmost;
    [ObservableProperty] private int _readerAutoHideSeconds = model.ReaderAutoHideSeconds;
    [ObservableProperty] private string _readerFontFamily = model.ReaderFontFamily;
    [ObservableProperty] private double _readerFontSize = model.ReaderFontSize;
    [ObservableProperty] private double _readerWindowOpacity = model.ReaderWindowOpacity;

    public bool IsClickTrigger
    {
        get => FloatingWidgetTriggerMode == FloatingWidgetTriggerMode.Click;
        set
        {
            if (value) FloatingWidgetTriggerMode = FloatingWidgetTriggerMode.Click;
        }
    }

    public bool IsPointerHoverTrigger
    {
        get => FloatingWidgetTriggerMode == FloatingWidgetTriggerMode.PointerHover;
        set
        {
            if (value) FloatingWidgetTriggerMode = FloatingWidgetTriggerMode.PointerHover;
        }
    }

    partial void OnFloatingWidgetTriggerModeChanged(FloatingWidgetTriggerMode value)
    {
        OnPropertyChanged(nameof(IsClickTrigger));
        OnPropertyChanged(nameof(IsPointerHoverTrigger));
    }

    partial void OnReaderAutoHideSecondsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 86400);
        if (value != clamped)
        {
            ReaderAutoHideSeconds = clamped;
        }
    }

    public void ApplyToModel()
    {
        Model.FloatingWidgetEnabled = FloatingWidgetEnabled;
        Model.FloatingWidgetTriggerMode = FloatingWidgetTriggerMode;
        Model.FloatingWidgetEdgeSnapEnabled = FloatingWidgetEdgeSnapEnabled;
        Model.ReaderWindowTopmost = ReaderWindowTopmost;
        Model.ReaderAutoHideSeconds = Math.Clamp(ReaderAutoHideSeconds, 0, 86400);
        Model.ReaderFontFamily = string.IsNullOrWhiteSpace(ReaderFontFamily) ? "Microsoft YaHei UI" : ReaderFontFamily;
        Model.ReaderFontSize = Math.Clamp(ReaderFontSize, 10, 48);
        Model.ReaderWindowOpacity = Math.Clamp(ReaderWindowOpacity, 0.25, 1);
    }
}
