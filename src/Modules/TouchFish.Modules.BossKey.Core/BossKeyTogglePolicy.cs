namespace TouchFish.Modules.BossKey;

public enum BossKeyToggleAction
{
    None,
    MinimizeAll,
    ShowAll
}

public static class BossKeyTogglePolicy
{
    public static BossKeyToggleAction Decide(IReadOnlyCollection<bool> minimizedStates)
    {
        if (minimizedStates.Count == 0)
        {
            return BossKeyToggleAction.None;
        }

        return minimizedStates.All(isMinimized => isMinimized)
            ? BossKeyToggleAction.ShowAll
            : BossKeyToggleAction.MinimizeAll;
    }
}
