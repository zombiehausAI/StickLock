using MudBlazor;

namespace StickLock.Components.Theme;

public static class StickLockTheme {
    public static MudTheme CreateTheme() {
        return new MudTheme {
            PaletteDark = new PaletteDark {
                Primary = "#00E5FF",
                Secondary = "#FFAB00",
                Tertiary = "#38BDF8",
                Background = "#0B0F19",
                Surface = "#111827",
                AppbarBackground = "#0B0F19",
                AppbarText = "#00E5FF",
                TextPrimary = "#E2E8F0",
                TextSecondary = "#94A3B8",
                ActionDefault = "#00E5FF",
                ActionDisabled = "#475569",
                ActionDisabledBackground = "#1E293B",
                Success = "#00E676",
                Warning = "#FFAB00",
                Error = "#FF5252",
                Info = "#00E5FF",
                LinesDefault = "#1E293B",
                TableLines = "#1E293B",
                Divider = "#1E293B",
                DrawerBackground = "#0B0F19"
            },
            LayoutProperties = new LayoutProperties {
                DefaultBorderRadius = "6px"
            }
        };
    }
}

