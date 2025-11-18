using System;

namespace VirtualCorkboard.Services
{
    // Basic theme manager for Core. Free/Premium can extend/register themes using pack URIs.
    public class ThemeManager : IThemeManager
    {
        public event EventHandler? ThemeChanged;
        public string CurrentThemeKey { get; private set; } = "Default";

        public void ApplyTheme(string themeKey)
        {
            // In Core we only raise events and manage a logical key.
            // Free/Premium shells are responsible for loading ResourceDictionaries.
            if (CurrentThemeKey == themeKey) return;
            CurrentThemeKey = themeKey;
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
