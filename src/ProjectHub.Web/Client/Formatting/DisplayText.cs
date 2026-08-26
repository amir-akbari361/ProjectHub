using System.Text;

namespace ProjectHub.Web.Client.Formatting;

/// <summary>
/// Presentation-only formatting helpers shared by the Blazor components.
/// </summary>
/// <remarks>
/// WHY DOES THIS EXIST INSTEAD OF FORMATTING INLINE IN EACH COMPONENT?
/// Enum names are written for code (<c>InProgress</c>), not for people ("In progress"). Rendering the raw
/// name leaks an identifier into the UI, and every component that fixed it locally would invent its own
/// spelling. One function means the Kanban column header, the task chip, and the filter dropdown all agree.
/// </remarks>
public static class DisplayText
{
    /// <summary>
    /// Splits a PascalCase identifier into words and sentence-cases the result:
    /// <c>InProgress</c> → "In progress", <c>Todo</c> → "Todo", <c>TaskStatusChanged</c> → "Task status changed".
    /// </summary>
    /// <remarks>
    /// Consecutive capitals are treated as one word so an acronym survives intact ("PDFExport" → "PDF export")
    /// rather than being exploded letter by letter.
    /// </remarks>
    public static string Humanize<TEnum>(TEnum value) where TEnum : struct, Enum =>
        Humanize(value.ToString() ?? string.Empty);

    /// <summary>String overload, for names that did not arrive as an enum (e.g. an audit log's action).</summary>
    public static string Humanize(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(identifier.Length + 8);

        for (var i = 0; i < identifier.Length; i++)
        {
            var current = identifier[i];

            // A boundary is an uppercase letter that follows a lowercase one ("inP") or that starts a new
            // word after an acronym ("PDFExport" → break before the 'E', because 'x' that follows is lower).
            var startsNewWord =
                i > 0
                && char.IsUpper(current)
                && (!char.IsUpper(identifier[i - 1])
                    || (i + 1 < identifier.Length && char.IsLower(identifier[i + 1])));

            if (startsNewWord)
            {
                builder.Append(' ');
                builder.Append(char.ToLowerInvariant(current));
                continue;
            }

            builder.Append(i == 0 ? char.ToUpperInvariant(current) : current);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Formats a byte count with binary units — one decimal place above KB, whole bytes below.
    /// </summary>
    /// <remarks>
    /// Written as if-statements rather than a switch expression with relational patterns on purpose: this
    /// helper is also called from Razor <c>@code</c> blocks, where a <c>&lt;</c> opening a statement is
    /// parsed as the start of a markup tag and breaks the whole file.
    /// </remarks>
    public static string FileSize(long bytes)
    {
        const long Kilobyte = 1024;
        const long Megabyte = Kilobyte * 1024;
        const long Gigabyte = Megabyte * 1024;

        if (bytes < Kilobyte)
        {
            return $"{bytes} B";
        }

        if (bytes < Megabyte)
        {
            return $"{bytes / (double)Kilobyte:0.#} KB";
        }

        if (bytes < Gigabyte)
        {
            return $"{bytes / (double)Megabyte:0.#} MB";
        }

        return $"{bytes / (double)Gigabyte:0.#} GB";
    }

    /// <summary>
    /// Renders a UTC instant as a compact relative age ("just now", "4m ago", "3d ago"), falling back to an
    /// absolute date once "days ago" stops being useful.
    /// </summary>
    /// <remarks>
    /// Notification and audit feeds are scanned, not read: "2m ago" answers "is this new?" at a glance, which
    /// a timestamp does not. Beyond a week the relative form loses precision without gaining meaning, so we
    /// switch to the date. The instant is converted to local time for the absolute branch because that is the
    /// only frame a reader can check against their own clock.
    /// </remarks>
    public static string RelativeTime(DateTime utc, DateTime? nowUtc = null)
    {
        var elapsed = (nowUtc ?? DateTime.UtcNow) - utc;

        if (elapsed < TimeSpan.Zero)
        {
            // Clock skew between the API host and this one. Reporting a negative age would look broken, so
            // we treat anything in the near future as having just happened.
            return "just now";
        }

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes}m ago";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return $"{(int)elapsed.TotalHours}h ago";
        }

        if (elapsed < TimeSpan.FromDays(7))
        {
            return $"{(int)elapsed.TotalDays}d ago";
        }

        return utc.ToLocalTime().ToString("d MMM yyyy");
    }
}
