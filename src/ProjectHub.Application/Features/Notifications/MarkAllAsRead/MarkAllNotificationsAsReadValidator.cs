using FluentValidation;

namespace ProjectHub.Application.Features.Notifications.MarkAllAsRead;

/// <summary>
/// Validator for <see cref="MarkAllNotificationsAsReadCommand"/>. The command has no fields, so there is
/// literally nothing to validate — the recipient is the authenticated principal, not client input. We
/// still declare an EMPTY validator so the type participates uniformly in the <c>ValidationBehavior</c>
/// pipeline (every command has a validator; none is a special case), and so a future field gets a natural
/// home instead of tempting an ad-hoc check in the handler.
/// </summary>
public sealed class MarkAllNotificationsAsReadValidator
    : AbstractValidator<MarkAllNotificationsAsReadCommand>
{
    public MarkAllNotificationsAsReadValidator()
    {
    }
}
