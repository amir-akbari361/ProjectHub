using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;

namespace ProjectHub.Application.Features.Notifications.EventHandlers;

/// <summary>
/// Notifies the assignee of a task when someone comments on it, so the person responsible for the work sees
/// the discussion. The comment's author is excluded — both here (via the event's <c>AuthorId</c>, which stays
/// correct even when there is no request principal, e.g. the seeder) and again by the base's actor exclusion.
/// </summary>
public sealed class CommentAddedNotificationHandler : NotificationEventHandler<CommentAddedDomainEvent>
{
    public CommentAddedNotificationHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<CommentAddedNotificationHandler> logger)
        : base(context, currentUser, dateTimeProvider, unitOfWork, logger)
    {
    }

    protected override async Task<IReadOnlyCollection<NotificationRequest>> ResolveAsync(
        CommentAddedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var task = await Context.ProjectTasks
            .AsNoTracking()
            .Where(task => task.Id == domainEvent.TaskId)
            .Select(task => new { task.AssigneeId, Title = task.Title.Value })
            .SingleOrDefaultAsync(cancellationToken);

        // Nobody to tell when the task is gone, has no assignee, or the commenter IS the assignee.
        if (task?.AssigneeId is not { } assigneeId || assigneeId == domainEvent.AuthorId)
        {
            return [];
        }

        return
        [
            new NotificationRequest(
                assigneeId,
                NotificationType.CommentAdded,
                $"New comment on your task \"{task.Title}\"."),
        ];
    }
}
