namespace TouchFish.Modules.Reader;

public static class ReaderWidgetPlacement
{
    public static (double Left, double Top) CenterWidgetOnWindow(
        double windowLeft,
        double windowTop,
        double windowWidth,
        double windowHeight,
        double widgetWidth,
        double widgetHeight) =>
        (
            windowLeft + (windowWidth - widgetWidth) / 2,
            windowTop + (windowHeight - widgetHeight) / 2
        );

    public static (double Left, double Top) CenterWindowOnWidget(
        double widgetLeft,
        double widgetTop,
        double widgetWidth,
        double widgetHeight,
        double windowWidth,
        double windowHeight) =>
        (
            widgetLeft + widgetWidth / 2 - windowWidth / 2,
            widgetTop + widgetHeight / 2 - windowHeight / 2
        );
}
