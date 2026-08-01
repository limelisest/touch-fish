using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TouchFish.UI.FloatingWidgets;

public static class FloatingWindowStyles
{
    private const int ExtendedStyleIndex = -20;
    private const long ToolWindowStyle = 0x00000080L;
    private const long AppWindowStyle = 0x00040000L;
    private const uint NoSize = 0x0001;
    private const uint NoMove = 0x0002;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;
    private const uint FrameChanged = 0x0020;

    public static void HideFromAltTab(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        var currentStyle = NativeMethods.GetWindowLongPtr(handle, ExtendedStyleIndex).ToInt64();
        var updatedStyle = (currentStyle | ToolWindowStyle) & ~AppWindowStyle;
        if (updatedStyle == currentStyle)
        {
            return;
        }

        NativeMethods.SetWindowLongPtr(handle, ExtendedStyleIndex, new nint(updatedStyle));
        NativeMethods.SetWindowPos(
            handle,
            nint.Zero,
            0,
            0,
            0,
            0,
            NoSize | NoMove | NoZOrder | NoActivate | FrameChanged);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        internal static extern nint GetWindowLongPtr(nint windowHandle, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        internal static extern nint SetWindowLongPtr(nint windowHandle, int index, nint newValue);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            nint windowHandle,
            nint insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);
    }
}
