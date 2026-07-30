using TouchFish.Contracts;
using Xunit;
using TouchFish.Modules.BossKey;

namespace TouchFish.Modules.BossKey.Tests;

public sealed class WindowRuleMatcherTests
{
    private readonly WindowRuleMatcher _matcher = new();

    [Fact]
    public void BrowserAppId_IsStableWhenTitleChanges()
    {
        var rule = Rule(browserAppId: "telegram-app-id", title: string.Empty);
        var window = Window(browserAppId: "telegram-app-id", title: "Alice (3 unread messages)");

        Assert.True(_matcher.IsMatch(rule, window));
    }

    [Fact]
    public void BrowserAppId_RejectsAnotherChromeApp()
    {
        var rule = Rule(browserAppId: "telegram-app-id", title: string.Empty);
        var window = Window(browserAppId: "another-app-id", title: "Telegram");

        Assert.False(_matcher.IsMatch(rule, window));
    }

    [Fact]
    public void OrdinaryWindow_UsesProcessClassAndTitle()
    {
        var rule = Rule(browserAppId: null, title: "QQ");

        Assert.True(_matcher.IsMatch(rule, Window(browserAppId: null, title: "QQ")));
        Assert.False(_matcher.IsMatch(rule, Window(browserAppId: null, title: "设置")));
    }

    private static WindowRule Rule(string? browserAppId, string title) => new()
    {
        Name = "test",
        ProcessPath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        ProcessName = "chrome",
        WindowClass = "Chrome_WidgetWin_1",
        TitleContains = title,
        BrowserAppId = browserAppId
    };

    private static WindowDescriptor Window(string? browserAppId, string title) => new(
        (nint)123,
        10,
        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        "chrome",
        "Chrome_WidgetWin_1",
        title,
        "Chrome.Default",
        browserAppId);
}
