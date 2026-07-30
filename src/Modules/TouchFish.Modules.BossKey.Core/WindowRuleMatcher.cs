using TouchFish.Contracts;

namespace TouchFish.Modules.BossKey;

public sealed class WindowRuleMatcher
{
    public bool IsMatch(WindowRule rule, WindowDescriptor window)
    {
        if (!MatchesProcess(rule, window))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.WindowClass) &&
            !string.Equals(rule.WindowClass, window.ClassName, StringComparison.Ordinal))
        {
            return false;
        }

        // Chrome/Edge 安装式 Web App 的 --app-id 是最稳定的窗口身份。
        if (!string.IsNullOrWhiteSpace(rule.BrowserAppId))
        {
            return string.Equals(rule.BrowserAppId, window.BrowserAppId, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(rule.AppUserModelId) &&
            !string.Equals(rule.AppUserModelId, window.AppUserModelId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(rule.TitleContains) ||
               window.Title.Contains(rule.TitleContains, StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<WindowDescriptor> FindMatches(
        WindowRule rule,
        IEnumerable<WindowDescriptor> windows) =>
        windows.Where(window => IsMatch(rule, window)).ToArray();

    private static bool MatchesProcess(WindowRule rule, WindowDescriptor window)
    {
        if (!string.IsNullOrWhiteSpace(rule.ProcessPath) && !string.IsNullOrWhiteSpace(window.ProcessPath))
        {
            return string.Equals(rule.ProcessPath, window.ProcessPath, StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(rule.ProcessName) &&
               string.Equals(rule.ProcessName, window.ProcessName, StringComparison.OrdinalIgnoreCase);
    }
}
