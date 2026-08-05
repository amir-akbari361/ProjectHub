using FluentValidation;

namespace ProjectHub.Application.Features.Notifications.ListNotifications;

/// <summary>
/// Validates the SHAPE of a <see cref="ListNotificationsQuery"/>. Clamps the paging inputs to safe bounds
/// so a client can never request page 0, a negative page, or an unbounded page size (which would let one
/// request pull the entire inbox and exhaust memory). There is nothing else to validate — the recipient is
/// not a client input, and <c>UnreadOnly</c> is a bool that cannot be out of range.
/// </summary>
public sealed class ListNotificationsValidator : AbstractValidator<ListNotificationsQuery>
{
    // Upper bound on a single page. Mirrors the ceiling used elsewhere for a consistent API contract.
    private const int MaxPageSize = 100;

    public ListNotificationsValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(MaxPageSize);
    }
}
