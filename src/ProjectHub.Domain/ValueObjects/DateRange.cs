using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.ValueObjects;

public sealed class DateRange : ValueObject
{
    private DateRange(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public int DurationInDays => (End - Start).Days;

    public static DateRange Create(DateTime start, DateTime end)
    {
        if (start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Date range boundaries must be expressed in UTC.");
        }

        if (end <= start)
        {
            throw new DomainException("Date range end must be after its start.");
        }

        return new DateRange(start, end);
    }

    public bool Contains(DateTime moment) => moment >= Start && moment <= End;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
