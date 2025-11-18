using System;

namespace VirtualCorkboard.Services
{
    public interface IThemeManager
    {
        event EventHandler? ThemeChanged;
        string CurrentThemeKey { get; }
        void ApplyTheme(string themeKey);
    }
}
