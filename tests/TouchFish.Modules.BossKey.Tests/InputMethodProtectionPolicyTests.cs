using TouchFish.Contracts;
using Xunit;

namespace TouchFish.Modules.BossKey.Tests;

public sealed class InputMethodProtectionPolicyTests
{
    [Fact]
    public void ImeBeingEnabledWithoutCompositionOrCandidateDoesNotProtect()
    {
        var reason = InputMethodProtectionPolicy.Resolve(
            hasActiveCompositionOrCandidateList: false,
            targetRecentlyFocused: true,
            foregroundIsInputMethodWindow: false,
            cursorIsInputMethodWindow: false);

        Assert.Equal(InputMethodProtectionReason.None, reason);
    }

    [Fact]
    public void ActiveCompositionProtectsTheTarget()
    {
        var reason = InputMethodProtectionPolicy.Resolve(
            hasActiveCompositionOrCandidateList: true,
            targetRecentlyFocused: true,
            foregroundIsInputMethodWindow: false,
            cursorIsInputMethodWindow: false);

        Assert.Equal(InputMethodProtectionReason.ActiveCompositionOrCandidateList, reason);
    }

    [Theory]
    [InlineData(true, false, InputMethodProtectionReason.ForegroundInputMethodWindow)]
    [InlineData(false, true, InputMethodProtectionReason.CursorInputMethodWindow)]
    public void AssociatedInputMethodWindowProtectsRecentlyFocusedTarget(
        bool foregroundIsInputMethodWindow,
        bool cursorIsInputMethodWindow,
        InputMethodProtectionReason expected)
    {
        var reason = InputMethodProtectionPolicy.Resolve(
            hasActiveCompositionOrCandidateList: false,
            targetRecentlyFocused: true,
            foregroundIsInputMethodWindow,
            cursorIsInputMethodWindow);

        Assert.Equal(expected, reason);
    }

    [Fact]
    public void UnrelatedInputMethodWindowDoesNotProtectStaleTarget()
    {
        var reason = InputMethodProtectionPolicy.Resolve(
            hasActiveCompositionOrCandidateList: false,
            targetRecentlyFocused: false,
            foregroundIsInputMethodWindow: true,
            cursorIsInputMethodWindow: true);

        Assert.Equal(InputMethodProtectionReason.None, reason);
    }
}
