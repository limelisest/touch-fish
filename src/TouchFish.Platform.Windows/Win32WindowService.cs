using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using TouchFish.Contracts;

namespace TouchFish.Platform.Windows;

public sealed partial class Win32WindowService : IWindowService
{
    private const uint GaRoot = 2;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int SwRestore = 9;
    private const int SwMinimize = 6;
    private const uint WmGetIcon = 0x007F;
    private const int GclpIcon = -14;
    private const int GclpIconSmall = -34;
    private const uint SmtoAbortIfHung = 0x0002;

    private static readonly PropertyKey AppUserModelIdKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    private static readonly PropertyKey RelaunchCommandKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 2);

    public WindowDescriptor? InspectAtScreenPoint(int x, int y)
    {
        var handle = NativeMethods.WindowFromPoint(new Point(x, y));
        if (handle == nint.Zero)
        {
            return null;
        }

        handle = NativeMethods.GetAncestor(handle, GaRoot);
        return Inspect(handle);
    }

    public IReadOnlyList<WindowDescriptor> EnumerateTopLevelWindows()
    {
        var result = new List<WindowDescriptor>();
        NativeMethods.EnumWindows((handle, _) =>
        {
            if (!NativeMethods.IsWindowVisible(handle) || NativeMethods.GetWindowTextLength(handle) == 0)
            {
                return true;
            }

            var descriptor = Inspect(handle);
            if (descriptor is not null)
            {
                result.Add(descriptor);
            }

            return true;
        }, nint.Zero);
        return result;
    }

    public nint GetForegroundWindowHandle() => NativeMethods.GetForegroundWindow();

    public byte[]? GetWindowIconPng(nint windowHandle)
    {
        try
        {
            nint iconHandle = nint.Zero;
            foreach (var iconType in new nuint[] { 2, 0, 1 })
            {
                NativeMethods.SendMessageTimeout(
                    windowHandle,
                    WmGetIcon,
                    iconType,
                    nint.Zero,
                    SmtoAbortIfHung,
                    100,
                    out var result);
                if (result != 0)
                {
                    iconHandle = (nint)result;
                    break;
                }
            }

            if (iconHandle == nint.Zero)
            {
                iconHandle = NativeMethods.GetClassLongPtr(windowHandle, GclpIconSmall);
            }

            if (iconHandle == nint.Zero)
            {
                iconHandle = NativeMethods.GetClassLongPtr(windowHandle, GclpIcon);
            }

            if (iconHandle == nint.Zero)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(24, 24));
            source.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    public bool TryFocus(nint windowHandle)
    {
        if (!NativeMethods.IsWindow(windowHandle))
        {
            return false;
        }

        if (NativeMethods.IsIconic(windowHandle))
        {
            NativeMethods.ShowWindowAsync(windowHandle, SwRestore);
        }

        NativeMethods.BringWindowToTop(windowHandle);
        return NativeMethods.SetForegroundWindow(windowHandle);
    }

    public bool IsWindow(nint windowHandle) => NativeMethods.IsWindow(windowHandle);

    public bool IsMinimized(nint windowHandle) => NativeMethods.IsIconic(windowHandle);

    public WindowPlacementSnapshot? CapturePlacement(nint windowHandle)
    {
        var placement = WindowPlacement.Create();
        if (!NativeMethods.GetWindowPlacement(windowHandle, ref placement))
        {
            return null;
        }

        return new WindowPlacementSnapshot(
            windowHandle,
            placement.Flags,
            placement.ShowCommand,
            placement.MinPosition.X,
            placement.MinPosition.Y,
            placement.MaxPosition.X,
            placement.MaxPosition.Y,
            placement.NormalPosition.Left,
            placement.NormalPosition.Top,
            placement.NormalPosition.Right,
            placement.NormalPosition.Bottom);
    }

    public bool Minimize(nint windowHandle) =>
        NativeMethods.IsWindow(windowHandle) && NativeMethods.ShowWindowAsync(windowHandle, SwMinimize);

    public bool Restore(nint windowHandle) =>
        NativeMethods.IsWindow(windowHandle) && NativeMethods.ShowWindowAsync(windowHandle, SwRestore);

    public bool Restore(WindowPlacementSnapshot snapshot)
    {
        if (!NativeMethods.IsWindow(snapshot.Handle))
        {
            return false;
        }

        var placement = WindowPlacement.Create();
        placement.Flags = snapshot.Flags;
        placement.ShowCommand = snapshot.ShowCommand;
        placement.MinPosition = new Point(snapshot.MinPositionX, snapshot.MinPositionY);
        placement.MaxPosition = new Point(snapshot.MaxPositionX, snapshot.MaxPositionY);
        placement.NormalPosition = new Rect(
            snapshot.Left,
            snapshot.Top,
            snapshot.Right,
            snapshot.Bottom);
        return NativeMethods.SetWindowPlacement(snapshot.Handle, ref placement);
    }

    private static WindowDescriptor? Inspect(nint handle)
    {
        try
        {
            if (handle == nint.Zero || !NativeMethods.IsWindow(handle))
            {
                return null;
            }

            NativeMethods.GetWindowThreadProcessId(handle, out var processId);
            var title = GetWindowText(handle);
            var className = GetClassName(handle);
            var processPath = GetProcessPath(processId);
            var processName = string.IsNullOrWhiteSpace(processPath)
                ? TryGetProcessName(processId)
                : Path.GetFileNameWithoutExtension(processPath);
            var appUserModelId = GetWindowProperty(handle, AppUserModelIdKey);
            var relaunchCommand = GetWindowProperty(handle, RelaunchCommandKey);
            var browserAppId = ExtractBrowserAppId(relaunchCommand);

            return new WindowDescriptor(
                handle,
                processId,
                processPath,
                processName,
                className,
                title,
                appUserModelId,
                browserAppId);
        }
        catch
        {
            // A single protected or malformed foreign window must not crash the application.
            return null;
        }
    }

    private static string GetWindowText(nint handle)
    {
        var length = NativeMethods.GetWindowTextLength(handle);
        var builder = new StringBuilder(Math.Max(length + 1, 2));
        NativeMethods.GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetClassName(nint handle)
    {
        var builder = new StringBuilder(256);
        NativeMethods.GetClassName(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetProcessPath(uint processId)
    {
        var process = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == nint.Zero)
        {
            return string.Empty;
        }

        try
        {
            var capacity = 32768u;
            var builder = new StringBuilder((int)capacity);
            return NativeMethods.QueryFullProcessImageName(process, 0, builder, ref capacity)
                ? builder.ToString()
                : string.Empty;
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    private static string TryGetProcessName(uint processId)
    {
        try
        {
            return Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? GetWindowProperty(nint handle, PropertyKey key)
    {
        IPropertyStore? propertyStore = null;
        try
        {
            var interfaceId = typeof(IPropertyStore).GUID;
            var result = NativeMethods.SHGetPropertyStoreForWindow(handle, ref interfaceId, out propertyStore);
            if (result < 0 || propertyStore is null)
            {
                return null;
            }

            var variant = new PropVariant();
            try
            {
                var propertyResult = propertyStore.GetValue(ref key, out variant);
                return propertyResult >= 0 ? variant.GetString() : null;
            }
            finally
            {
                NativeMethods.PropVariantClear(ref variant);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (propertyStore is not null && Marshal.IsComObject(propertyStore))
            {
                Marshal.FinalReleaseComObject(propertyStore);
            }
        }
    }

    internal static string? ExtractBrowserAppId(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var appIdMatch = BrowserAppIdRegex().Match(command);
        if (appIdMatch.Success)
        {
            return appIdMatch.Groups["value"].Value.Trim('"');
        }

        var appUrlMatch = BrowserAppUrlRegex().Match(command);
        return appUrlMatch.Success
            ? appUrlMatch.Groups["value"].Value.Trim('"')
            : null;
    }

    [GeneratedRegex("--app-id(?:=|\\s+)(?<value>\\\"[^\\\"]+\\\"|\\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex BrowserAppIdRegex();

    [GeneratedRegex("--app(?:=|\\s+)(?<value>\\\"[^\\\"]+\\\"|\\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex BrowserAppUrlRegex();

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Point(int X, int Y);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Rect(int Left, int Top, int Right, int Bottom);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacement
    {
        public int Length;
        public int Flags;
        public int ShowCommand;
        public Point MinPosition;
        public Point MaxPosition;
        public Rect NormalPosition;

        public static WindowPlacement Create() => new()
        {
            Length = Marshal.SizeOf<WindowPlacement>()
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey(Guid formatId, uint propertyId)
    {
        public Guid FormatId = formatId;
        public uint PropertyId = propertyId;
    }

    // PROPVARIANT is 24 bytes on 64-bit Windows. An undersized declaration can
    // overwrite managed memory when IPropertyStore.GetValue fills the value.
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] private readonly ushort _variantType;
        [FieldOffset(8)] private readonly nint _pointer;

        public string? GetString() => _variantType switch
        {
            8 => Marshal.PtrToStringBSTR(_pointer),
            31 => Marshal.PtrToStringUni(_pointer),
            _ => null
        };
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint propertyCount);
        [PreserveSig] int GetAt(uint propertyIndex, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    private static class NativeMethods
    {
        internal delegate bool EnumWindowsCallback(nint windowHandle, nint parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

        [DllImport("user32.dll")]
        internal static extern nint WindowFromPoint(Point point);

        [DllImport("user32.dll")]
        internal static extern nint GetAncestor(nint windowHandle, uint flags);

        [DllImport("user32.dll")]
        internal static extern nint GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern nint SendMessageTimeout(
            nint windowHandle,
            uint message,
            nuint wParam,
            nint lParam,
            uint flags,
            uint timeout,
            out nuint result);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW", SetLastError = true)]
        internal static extern nint GetClassLongPtr(nint windowHandle, int index);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(nint windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(nint windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(nint windowHandle);

        [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowTextLength(nint windowHandle);

        [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(nint windowHandle, StringBuilder text, int maximumCount);

        [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(nint windowHandle, StringBuilder className, int maximumCount);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindowAsync(nint windowHandle, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(nint windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BringWindowToTop(nint windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowPlacement(nint windowHandle, ref WindowPlacement placement);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPlacement(nint windowHandle, ref WindowPlacement placement);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref uint size);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(nint handle);

        [DllImport("shell32.dll")]
        internal static extern int SHGetPropertyStoreForWindow(
            nint windowHandle,
            ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? propertyStore);

        [DllImport("ole32.dll")]
        internal static extern int PropVariantClear(ref PropVariant variant);
    }
}
