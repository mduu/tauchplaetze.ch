using MudBlazor;

namespace Tauchplaetze.Web.Components.Layout;

/// <summary>Dunkles Theme des CMS aus den Design Tokens (`design-system.md`, «Kaltwasser-Nacht»).</summary>
internal static class AdminTheme
{
    public static readonly MudTheme Dark = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#7FE3D6",
            PrimaryContrastText = "#071417",
            Secondary = "#FF9A4D",
            SecondaryContrastText = "#071417",
            Background = "#071417",
            Surface = "#0A1D20",
            AppbarBackground = "#0A1D20",
            DrawerBackground = "#0A1D20",
            TextPrimary = "#E4EFEC",
            TextSecondary = "#A9C2BE",
            LinesDefault = "#1D3E41",
            Divider = "#16312F",
            Warning = "#FF9A4D",
            Success = "#7FE3D6",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["IBM Plex Sans", "system-ui", "sans-serif"] },
            H4 = new H4Typography { FontFamily = ["Bricolage Grotesque", "system-ui", "sans-serif"], FontWeight = "800" },
            H5 = new H5Typography { FontFamily = ["Bricolage Grotesque", "system-ui", "sans-serif"], FontWeight = "700" },
            H6 = new H6Typography { FontFamily = ["Bricolage Grotesque", "system-ui", "sans-serif"], FontWeight = "700" },
        },
    };
}
