using System.ComponentModel;
using System.Runtime.InteropServices;
using TouchFish.Contracts;

namespace TouchFish.Platform.Windows;

public sealed class Win32WindowPickerService(IWindowService windowService) : IWindowPickerService
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const uint WmKeyDown = 0x0100;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmLeftButtonDown = 0x0201;
    private const uint WmQuit = 0x0012;
    private const uint VkEscape = 0x1B;

    public async Task<WindowDescriptor?> PickWindowAsync(CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<ScreenPoint?>(TaskCreationOptions.RunContinuationsAsynchronously);
        uint hookThreadId = 0;

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            var threadId = Volatile.Read(ref hookThreadId);
            if (threadId != 0)
            {
                NativeMethods.PostThreadMessage(threadId, WmQuit, nint.Zero, nint.Zero);
            }
        });

        var hookThread = new Thread(() =>
        {
            nint mouseHook = nint.Zero;
            nint keyboardHook = nint.Zero;
            ScreenPoint? selectedPoint = null;
            NativeMethods.HookProcedure? mouseProcedure = null;
            NativeMethods.HookProcedure? keyboardProcedure = null;

            try
            {
                hookThreadId = NativeMethods.GetCurrentThreadId();

                mouseProcedure = (code, message, data) =>
                {
                    if (code >= 0 && (uint)message == WmLeftButtonDown)
                    {
                        var mouse = Marshal.PtrToStructure<LowLevelMouseHook>(data);
                        selectedPoint = new ScreenPoint(mouse.Point.X, mouse.Point.Y);
                        NativeMethods.PostThreadMessage(hookThreadId, WmQuit, nint.Zero, nint.Zero);
                        return (nint)1;
                    }

                    return NativeMethods.CallNextHookEx(nint.Zero, code, message, data);
                };

                keyboardProcedure = (code, message, data) =>
                {
                    if (code >= 0 && ((uint)message == WmKeyDown || (uint)message == WmSysKeyDown))
                    {
                        var keyboard = Marshal.PtrToStructure<LowLevelKeyboardHook>(data);
                        if (keyboard.VirtualKey == VkEscape)
                        {
                            selectedPoint = null;
                            NativeMethods.PostThreadMessage(hookThreadId, WmQuit, nint.Zero, nint.Zero);
                            return (nint)1;
                        }
                    }

                    return NativeMethods.CallNextHookEx(nint.Zero, code, message, data);
                };

                var module = NativeMethods.GetModuleHandle(null);
                mouseHook = NativeMethods.SetWindowsHookEx(WhMouseLl, mouseProcedure, module, 0);
                keyboardHook = NativeMethods.SetWindowsHookEx(WhKeyboardLl, keyboardProcedure, module, 0);
                if (mouseHook == nint.Zero || keyboardHook == nint.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "无法进入窗口选择模式。");
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    while (NativeMethods.GetMessage(out var message, nint.Zero, 0, 0) > 0)
                    {
                        NativeMethods.TranslateMessage(ref message);
                        NativeMethods.DispatchMessage(ref message);
                    }
                }

                completion.TrySetResult(selectedPoint);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                if (mouseHook != nint.Zero)
                {
                    NativeMethods.UnhookWindowsHookEx(mouseHook);
                }

                if (keyboardHook != nint.Zero)
                {
                    NativeMethods.UnhookWindowsHookEx(keyboardHook);
                }

                GC.KeepAlive(mouseProcedure);
                GC.KeepAlive(keyboardProcedure);
            }
        })
        {
            IsBackground = true,
            Name = "TouchFish.WindowPicker"
        };
        hookThread.Start();

        var point = await completion.Task;
        return point is null
            ? null
            : windowService.InspectAtScreenPoint(point.Value.X, point.Value.Y);
    }

    private readonly record struct ScreenPoint(int X, int Y);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativePoint(int X, int Y);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct LowLevelMouseHook
    {
        public NativePoint Point { get; init; }
        public uint MouseData { get; init; }
        public uint Flags { get; init; }
        public uint Time { get; init; }
        public nuint ExtraInfo { get; init; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct LowLevelKeyboardHook
    {
        public uint VirtualKey { get; init; }
        public uint ScanCode { get; init; }
        public uint Flags { get; init; }
        public uint Time { get; init; }
        public nuint ExtraInfo { get; init; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint WindowHandle;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public NativePoint Point;
        public uint Private;
    }

    private static class NativeMethods
    {
        internal delegate nint HookProcedure(int code, nint message, nint data);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern nint SetWindowsHookEx(
            int hookId,
            HookProcedure callback,
            nint module,
            uint threadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(nint hook);

        [DllImport("user32.dll")]
        internal static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int GetMessage(out NativeMessage message, nint window, uint minimum, uint maximum);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TranslateMessage(ref NativeMessage message);

        [DllImport("user32.dll")]
        internal static extern nint DispatchMessage(ref NativeMessage message);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostThreadMessage(uint threadId, uint message, nint wParam, nint lParam);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern nint GetModuleHandle(string? moduleName);
    }
}
