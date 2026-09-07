using System.Globalization;
using System.Text;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Builds a URL query string from optional parameters, escaping values and omitting the ones that are null.
/// </summary>
/// <remarks>
/// WHY THIS EXISTS
/// Four typed clients each hand-rolled the same pattern:
/// <code>
/// var query = $"?pageNumber={p}&amp;pageSize={s}";
/// if (!string.IsNullOrEmpty(term)) query += $"&amp;searchTerm={Uri.EscapeDataString(term)}";
/// if (status is not null)         query += $"&amp;status={status}";
/// </code>
/// Four copies of that is a DRY violation with two live bugs baked in. First, escaping was applied by hand and
/// therefore inconsistently — the search term was escaped in one client and not another, so a term containing
/// <c>&amp;</c> or <c>#</c> silently truncated the request. Second, <c>bool</c> was appended with C#'s default
/// formatting ("True"), and a filter was often appended ONLY when true, which cannot express "explicitly
/// false" to an endpoint whose own default is true.
///
/// Centralising it means one place enforces: escape everything, use invariant lowercase for booleans (the
/// form ASP.NET's binder expects), send enums by NAME (the binder accepts names and they survive a renumbering
/// of the enum), and treat null as "omit" so the server's own default applies.
///
/// WHY A BUILDER AND NOT A DICTIONARY + JOIN?
/// Ordering is stable and the call site reads as a declaration of the request. A dictionary would also reject
/// the (legal, occasionally useful) case of repeating a key, and would need its own null-filtering pass anyway.
/// </remarks>
internal sealed class QueryStringBuilder
{
    private readonly StringBuilder _builder = new();

    /// <summary>Appends <c>name=value</c> unless <paramref name="value"/> is null or blank.</summary>
    public QueryStringBuilder Add(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return this;
        }

        return Append(name, Uri.EscapeDataString(value));
    }

    /// <summary>Appends an integer. Formatted invariantly so a comma-decimal culture cannot corrupt it.</summary>
    public QueryStringBuilder Add(string name, int value) =>
        Append(name, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Appends a boolean as lowercase <c>true</c>/<c>false</c>. Always appended, never conditionally: an
    /// endpoint that defaults a flag to <c>true</c> can only be told otherwise by sending it explicitly.
    /// </summary>
    public QueryStringBuilder Add(string name, bool value) =>
        Append(name, value ? "true" : "false");

    /// <summary>Appends a GUID unless null.</summary>
    public QueryStringBuilder Add(string name, Guid? value) =>
        value is { } guid ? Append(name, guid.ToString()) : this;

    /// <summary>
    /// Appends a timestamp unless null, using the round-trip ("O") format.
    /// </summary>
    /// <remarks>
    /// Round-trip rather than the culture's default: "O" is unambiguous, preserves the full precision and the
    /// UTC designator, and is a format ASP.NET's model binder parses without help. Formatting a date with the
    /// ambient culture is precisely the class of bug this builder exists to prevent — on a machine with a
    /// day-first culture, <c>ToString()</c> produces a value the binder reads month-first, silently shifting a
    /// date filter by weeks.
    /// </remarks>
    public QueryStringBuilder Add(string name, DateTime? value) =>
        value is { } timestamp
            ? Append(name, Uri.EscapeDataString(timestamp.ToString("O", CultureInfo.InvariantCulture)))
            : this;

    /// <summary>
    /// Appends a nullable enum by NAME unless null. Names rather than numbers because a name is readable in a
    /// log or a browser address bar, and it keeps working if the enum's underlying values are ever renumbered.
    /// </summary>
    public QueryStringBuilder Add<TEnum>(string name, TEnum? value) where TEnum : struct, Enum =>
        value is { } enumValue ? Append(name, enumValue.ToString()) : this;

    /// <summary>Appends a non-nullable enum by name.</summary>
    public QueryStringBuilder Add<TEnum>(string name, TEnum value) where TEnum : struct, Enum =>
        Append(name, value.ToString());

    /// <summary>
    /// Renders the accumulated pairs, including the leading <c>?</c>. Returns an EMPTY string when nothing was
    /// added, so callers can concatenate unconditionally without producing a trailing bare <c>?</c>.
    /// </summary>
    public string Build() => _builder.Length == 0 ? string.Empty : $"?{_builder}";

    private QueryStringBuilder Append(string name, string escapedValue)
    {
        if (_builder.Length > 0)
        {
            _builder.Append('&');
        }

        _builder.Append(Uri.EscapeDataString(name)).Append('=').Append(escapedValue);
        return this;
    }
}
