using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;

namespace ProjectHub.Application.Features.Notifications.EventHandlers;

/// <summary>
/// Notifies the assignee when a task is assigned to them. The assignee is carried directly on the event;
/// the base drops the self-assignment case (assigning a task to yourself needs no notification).
/// </summary>
public sealed class TaskAssignedNotificationHandler : NotificationEventHandler<TaskAssignedDomainEvent>
{
    public TaskAssignedNotificationHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<TaskAssignedNotificationHandler> logger)
        : base(context, currentUser, dateTimeProvider, unitOfWork, logger)
    {
    }

    protected override async Task<IReadOnlyCollection<NotificationRequest>> ResolveAsync(
        TaskAssignedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // Look up the title purely for a human-readable message. A null means the task vanished (soft-deleted
        // in a later, racing operation) — nothing worth notifying about.
        var title = await Context.ProjectTasks
            .AsNoTracking()
            .Where(task => task.Id == domainEvent.TaskId)
            .Select(task => task.Title.Value)
            .SingleOrDefaultAsync(cancellationToken);

        if (title is null)
        {
            return [];
        }

        return
        [
            new NotificationRequest(
                domainEvent.AssigneeId,
                NotificationType.TaskAssigned,
                $"You were assigned the task \"{title}\"."),
        ];
    }
}
