namespace TouchFish.Modules.BossKey;

public static class BossKeySettingsMigration
{
    public static BossKeySettings Migrate(BossKeySettings settings)
    {
        if (settings.SchemaVersion < 3)
        {
            foreach (var window in settings.Windows)
            {
                var legacyMinutes = Math.Clamp(window.LegacyAutoMinimizeMinutes ?? 1, 0, 1440);
                window.AutoMinimizeSeconds = legacyMinutes * 60;
                window.LegacyAutoMinimizeMinutes = null;
            }
        }

        settings.SchemaVersion = 4;
        return settings;
    }
}
