using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using TouchFish.Contracts;

namespace TouchFish.Platform.Windows;

public sealed class Win32HotkeyService : IHotkeyService
{
    private const int WmHotkey = 0x0312;
    private readonly Dictionary<string, Registration> _registrations = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Registration> _registrationsById = [];
    private HwndSource? _source;
    private nint _windowHandle;
    private int _nextId = 0x5400;

    public void Attach(nint windowHandle)
    {
        if (_source is not null)
        {
            return;
        }

        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle)
            ?? throw new InvalidOperationException("无法连接 TouchFish 主窗口的消息循环。");
        _source.AddHook(WndProc);
    }

    public bool TryRegister(string owner, HotkeyGesture gesture, Action callback, out string? error)
    {
        if (_source is null)
        {
            error = "快捷键服务尚未初始化。";
            return false;
        }

        if (gesture.VirtualKey == 0x7B)
        {
            error = "F12 由 Windows 保留，不能注册为全局快捷键。";
            return false;
        }

        if ((gesture.Modifiers & ~HotkeyModifiers.NoRepeat) == HotkeyModifiers.None)
        {
            error = "请至少按下 Ctrl、Alt、Shift 或 Win 中的一个修饰键。";
            return false;
        }

        if (_registrations.TryGetValue(owner, out var current) && current.Gesture == gesture)
        {
            current.Callback = callback;
            error = null;
            return true;
        }

        var id = _nextId++;
        var modifiers = gesture.Modifiers | HotkeyModifiers.NoRepeat;
        if (!NativeMethods.RegisterHotKey(_windowHandle, id, (uint)modifiers, (uint)gesture.VirtualKey))
        {
            error = $"快捷键注册失败：{new Win32Exception(Marshal.GetLastWin32Error()).Message}";
            return false;
        }

        if (current is not null)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, current.Id);
            _registrationsById.Remove(current.Id);
        }

        var registration = new Registration(id, gesture, callback);
        _registrations[owner] = registration;
        _registrationsById[id] = registration;
        error = null;
        return true;
    }

    public void Unregister(string owner)
    {
        if (!_registrations.Remove(owner, out var registration))
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_windowHandle, registration.Id);
        _registrationsById.Remove(registration.Id);
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotkey && _registrationsById.TryGetValue(wParam.ToInt32(), out var registration))
        {
            handled = true;
            registration.Callback();
        }

        return nint.Zero;
    }

    public void Dispose()
    {
        foreach (var registration in _registrations.Values)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, registration.Id);
        }

        _registrations.Clear();
        _registrationsById.Clear();
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private sealed class Registration(int id, HotkeyGesture gesture, Action callback)
    {
        public int Id { get; } = id;
        public HotkeyGesture Gesture { get; } = gesture;
        public Action Callback { get; set; } = callback;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(nint windowHandle, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(nint windowHandle, int id);
    }
}
