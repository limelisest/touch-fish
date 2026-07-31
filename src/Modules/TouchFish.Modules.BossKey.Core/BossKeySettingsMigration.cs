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

        if (settings.SchemaVersion < 5)
        {
            foreach (var window in settings.Windows)
            {
                // In older schemas zero meant disabled. Preserve that intent while
                // allowing zero to mean immediate once the new switch is enabled.
                window.AutoMinimizeEnabled = window.AutoMinimizeSeconds > 0;
            }
        }

        settings.SchemaVersion = 5;
        return settings;
    }
}
