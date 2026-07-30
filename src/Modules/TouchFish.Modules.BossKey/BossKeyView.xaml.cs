using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TouchFish.Contracts;

namespace TouchFish.Modules.BossKey;

public partial class BossKeyView : UserControl
{
    private bool _isPicking;
    private WindowDescriptor? _previewWindow;

    public BossKeyView()
    {
        InitializeComponent();
    }

    private BossKeyViewModel? ViewModel => DataContext as BossKeyViewModel;

    private void HotkeyCapture_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HotkeyCapture.Focus();
        Keyboard.Focus(HotkeyCapture);
        HotkeyCaptureText.Text = "请按下新快捷键……";
        e.Handled = true;
    }

    private async void HotkeyCapture_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            HotkeyCaptureText.SetBinding(TextBlock.TextProperty, "HotkeyText");
            Keyboard.ClearFocus();
            e.Handled = true;
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyModifiers.Windows;

        var gesture = new HotkeyGesture(KeyInterop.VirtualKeyFromKey(key), modifiers, GetKeyName(key));
        await ViewModel.SetHotkeyAsync(gesture);
        HotkeyCaptureText.SetBinding(TextBlock.TextProperty, "HotkeyText");
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void PickerButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPicking = true;
        _previewWindow = null;
        PickerButton.CaptureMouse();
        Mouse.OverrideCursor = Cursors.Cross;
        e.Handled = true;
    }

    private void PickerButton_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPicking || ViewModel is null)
        {
            return;
        }

        var screenPoint = PickerButton.PointToScreen(e.GetPosition(PickerButton));
        _previewWindow = ViewModel.PreviewAt((int)screenPoint.X, (int)screenPoint.Y);
    }

    private async void PickerButton_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPicking || ViewModel is null)
        {
            return;
        }

        var screenPoint = PickerButton.PointToScreen(e.GetPosition(PickerButton));
        _previewWindow = ViewModel.PreviewAt((int)screenPoint.X, (int)screenPoint.Y);
        EndPicking();
        await ViewModel.AddWindowAsync(_previewWindow);
        e.Handled = true;
    }

    private void PickerButton_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isPicking)
        {
            EndPicking();
        }
    }

    private void EndPicking()
    {
        _isPicking = false;
        Mouse.OverrideCursor = null;
        if (Mouse.Captured == PickerButton)
        {
            PickerButton.ReleaseMouseCapture();
        }
    }

    private static string GetKeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => $"Num {(int)(key - Key.NumPad0)}",
        Key.OemPlus => "+",
        Key.OemMinus => "-",
        _ => key.ToString()
    };
}
