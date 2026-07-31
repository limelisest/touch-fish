namespace TouchFish.Contracts;

public sealed record WindowDescriptor(
    nint Handle,
    uint ProcessId,
    string ProcessPath,
    string ProcessName,
    string ClassName,
    string Title,
    string? AppUserModelId,
    string? BrowserAppId);

public sealed record WindowPlacementSnapshot(
    nint Handle,
    int Flags,
    int ShowCommand,
    int MinPositionX,
    int MinPositionY,
    int MaxPositionX,
    int MaxPositionY,
    int Left,
    int Top,
    int Right,
    int Bottom);

public interface IWindowPickerService
{
    Task<WindowDescriptor?> PickWindowAsync(CancellationToken cancellationToken = default);
}

public interface IWindowService
{
    WindowDescriptor? InspectAtScreenPoint(int x, int y);
    WindowDescriptor? InspectWindow(nint windowHandle);
    IReadOnlyList<WindowDescriptor> EnumerateTopLevelWindows();
    nint GetForegroundWindowHandle();
    nint GetWindowUnderCursorHandle();
    bool IsWindowRelated(nint windowHandle, nint candidateHandle);
    byte[]? GetWindowIconPng(nint windowHandle);
    bool TryFocus(nint windowHandle);
    bool IsWindow(nint windowHandle);
    bool IsMinimized(nint windowHandle);
    WindowPlacementSnapshot? CapturePlacement(nint windowHandle);
    bool Minimize(nint windowHandle);
    bool Restore(nint windowHandle);
    bool Restore(WindowPlacementSnapshot placement);
}
