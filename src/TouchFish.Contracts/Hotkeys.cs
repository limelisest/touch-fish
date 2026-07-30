namespace TouchFish.Contracts;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
    NoRepeat = 0x4000
}

public sealed record HotkeyGesture(int VirtualKey, HotkeyModifiers Modifiers, string KeyName)
{
    public string DisplayName
    {
        get
        {
            var parts = new List<string>(4);
            if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
            parts.Add(KeyName);
            return string.Join(" + ", parts);
        }
    }
}

public interface IHotkeyService : IDisposable
{
    void Attach(nint windowHandle);
    bool TryRegister(string owner, HotkeyGesture gesture, Action callback, out string? error);
    void Unregister(string owner);
}
