using MudBlazor;

namespace ProjectHub.Web.Client.Theme;

/// <summary>
/// The application's SINGLE MudBlazor theme definition — brand palette (light + dark) and layout metrics.
/// Every layout binds this one instance instead of declaring its own copy.
/// </summary>
/// <remarks>
/// WHY A STATIC CLASS RATHER THAN A FIELD ON EACH LAYOUT?
/// The palette was previously declared twice, inline, in <c>MainLayout</c> and <c>AuthLayout</c>. Two copies
/// of ~40 colour literals is a DRY violation with a predictable failure mode: a rebrand updates one and the
/// sign-in screen silently drifts from the rest of the product. Hoisting it here gives the design tokens a
/// single source of truth, so a layout's only remaining job is composition (chrome, slots) — not styling.
///
/// WHY IS THE INSTANCE SHARED AND NOT CLONED PER CIRCUIT?
/// <see cref="MudTheme"/> is read-only configuration: MudThemeProvider only ever READS it to emit CSS
/// variables. The mutable part of theming — whether dark mode is active — lives per-user in
/// <see cref="ThemePreferenceStore"/>, never on the theme itself. So one immutable instance is safely
/// shared by every circuit and costs a single allocation for the process.
/// </remarks>
public static class ProjectHubTheme
{
    /// <summary>The shared brand theme. Bind to <c>MudThemeProvider.Theme</c>.</summary>
    public static MudTheme Instance { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#6366F1",       // indigo
            Secondary = "#8B5CF6",     // violet
            Tertiary = "#06B6D4",      // cyan
            AppbarBackground = "#FFFFFF",
            Background = "#F7F8FC",
            BackgroundGray = "#EEF0F7",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#334155",
            Success = "#10B981",
            Warning = "#F59E0B",
            Error = "#EF4444",
            Info = "#3B82F6",
            TextPrimary = "#1E293B",
            TextSecondary = "#64748B"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#818CF8",       // lighter indigo so it still reads on a dark surface
            Secondary = "#A78BFA",
            Tertiary = "#22D3EE",
            AppbarBackground = "#0F172A",
            Background = "#0B1120",
            BackgroundGray = "#111827",
            Surface = "#161E2E",
            DrawerBackground = "#0F172A",
            DrawerText = "#CBD5E1",
            Success = "#34D399",
            Warning = "#FBBF24",
            Error = "#F87171",
            Info = "#60A5FA",
            TextPrimary = "#E2E8F0",
            TextSecondary = "#94A3B8",
            LinesDefault = "#1E293B",
            TableLines = "#1E293B"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px",
            DrawerWidthLeft = "260px"
        }
    };

    /// <summary>
    /// Priority accent colours, used for the thin bar on a task card and the priority chip. Exposed here —
    /// not duplicated in each component — because two places rendering "High" in different oranges is the
    /// kind of inconsistency users notice and reviewers miss.
    /// </summary>
    public static string PriorityColor(Domain.Enums.TaskPriority priority) => priority switch
    {
        Domain.Enums.TaskPriority.Critical => "#EF4444",
        Domain.Enums.TaskPriority.High => "#F59E0B",
        Domain.Enums.TaskPriority.Medium => "#3B82F6",
        Domain.Enums.TaskPriority.Low => "#10B981",
        _ => "#94A3B8"
    };
}
