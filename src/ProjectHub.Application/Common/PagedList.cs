namespace ProjectHub.Application.Common;

/// <summary>
/// A single page of results plus the metadata a client needs to render pagination controls.
/// Returned by list queries so the read side never streams an entire table over the wire — the
/// caller asks for a page number and size, we return exactly that slice plus the total count.
/// </summary>
/// <remarks>
/// WHY A DEDICATED TYPE AND NOT <c>(IReadOnlyList&lt;T&gt;, int)</c>?
/// A named type documents intent, gives the API a stable JSON shape, and lets us compute derived
/// flags (<see cref="HasNextPage"/>, <see cref="HasPreviousPage"/>) in ONE place instead of every
/// caller re-deriving them and getting the off-by-one wrong.
///
/// WHY IS THE PROJECTION (mapping to <typeparamref name="T"/>) DONE BEFORE THIS TYPE EXISTS?
/// We build the page from already-projected DTOs, never from tracked entities. That keeps the read
/// side allocation-light and prevents accidental lazy-loading of navigation properties.
/// </remarks>
public sealed class PagedList<T>
{
    public PagedList(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    /// <summary>The items on the current page. Never null; an empty page returns an empty list.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Total number of rows matching the filter across ALL pages — the denominator for page counts.</summary>
    public int TotalCount { get; }

    /// <summary>The 1-based index of the current page.</summary>
    public int PageNumber { get; }

    /// <summary>The maximum number of items a page may contain.</summary>
    public int PageSize { get; }

    /// <summary>Total number of pages, computed with a ceiling division so a partial last page still counts.</summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;
}
