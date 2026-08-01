using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TouchFish.Modules.Reader;

public partial class ReaderView : System.Windows.Controls.UserControl
{
    public ReaderView()
    {
        InitializeComponent();
        Loaded += (_, _) => ScheduleSelectedChapterScroll();
    }

    private void ChapterList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ScheduleSelectedChapterScroll();

    private void ScheduleSelectedChapterScroll()
    {
        var selectedChapter = ChapterList.SelectedItem;
        if (selectedChapter is null)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (IsVisible && ReferenceEquals(ChapterList.SelectedItem, selectedChapter))
                {
                    ChapterList.ScrollIntoView(selectedChapter);
                }
            }));
    }
}
