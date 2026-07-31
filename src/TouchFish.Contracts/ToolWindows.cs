namespace TouchFish.Contracts;

public enum FloatingWidgetTriggerMode
{
    Click,
    PointerHover
}

public interface IManagedToolWindow
{
    string Id { get; }
    bool IsAvailable { get; }
    bool IsMinimizedOrHidden { get; }
    bool Minimize();
    bool Restore();
}

public interface IToolWindowRegistry
{
    IReadOnlyList<IManagedToolWindow> Windows { get; }
    void Register(IManagedToolWindow window);
    void Unregister(string id);
}

public sealed class ToolWindowRegistry : IToolWindowRegistry
{
    private readonly Dictionary<string, IManagedToolWindow> _windows = new(StringComparer.Ordinal);

    public IReadOnlyList<IManagedToolWindow> Windows => _windows.Values.ToArray();

    public void Register(IManagedToolWindow window) => _windows[window.Id] = window;

    public void Unregister(string id) => _windows.Remove(id);
}
