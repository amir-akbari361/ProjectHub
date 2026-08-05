using FluentValidation;

namespace ProjectHub.Application.Features.Notifications.MarkAsRead;

/// <summary>
/// Validates the SHAPE of a <see cref="MarkNotificationAsReadCommand"/>. The only client input is the
/// target id, so we simply guard it against the empty GUID. Ownership ("is this MY notification?") is a
/// BUSINESS concern resolved in the handler, not here.
/// </summary>
public sealed class MarkNotificationAsReadValidator : AbstractValidator<MarkNotificationAsReadCommand>
{
    public MarkNotificationAsReadValidator()
    {
        RuleFor(command => command.NotificationId)
            .NotEmpty();
    }
}
