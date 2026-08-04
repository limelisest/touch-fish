using System.Globalization;

namespace TouchFish.Contracts;

public enum InputMethodProtectionReason
{
    None,
    ActiveCompositionOrCandidateList,
    ForegroundInputMethodWindow,
    CursorInputMethodWindow
}

public static class InputMethodProtectionPolicy
{
    public static InputMethodProtectionReason Resolve(
        bool hasActiveCompositionOrCandidateList,
        bool targetRecentlyFocused,
        bool foregroundIsInputMethodWindow,
        bool cursorIsInputMethodWindow)
    {
        if (hasActiveCompositionOrCandidateList)
        {
            return InputMethodProtectionReason.ActiveCompositionOrCandidateList;
        }

        if (!targetRecentlyFocused)
        {
            return InputMethodProtectionReason.None;
        }

        if (foregroundIsInputMethodWindow)
        {
            return InputMethodProtectionReason.ForegroundInputMethodWindow;
        }

        return cursorIsInputMethodWindow
            ? InputMethodProtectionReason.CursorInputMethodWindow
            : InputMethodProtectionReason.None;
    }
}

public static class RuntimeDiagnosticLog
{
    private static readonly object SyncRoot = new();

    public static void Write(string category, string message)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "LimeLisest",
                "TouchFish",
                "log");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"runtime-{DateTime.Now:yyyyMMdd}.log");
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{category}] {message}{Environment.NewLine}");
            lock (SyncRoot)
            {
                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // Diagnostics must never affect floating-window behavior.
        }
    }
}
