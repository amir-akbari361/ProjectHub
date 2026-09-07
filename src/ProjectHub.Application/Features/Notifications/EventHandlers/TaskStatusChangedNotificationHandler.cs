using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;

namespace ProjectHub.Application.Features.Notifications.EventHandlers;

/// <summary>
/// Notifies the task's assignee when its status changes (e.g. moved into review or done), so the owner of the
/// work tracks its progress. If the assignee is the one who moved it, the base's actor exclusion drops it.
/// </summary>
public sealed class TaskStatusChangedNotificationHandler
    : NotificationEventHandler<TaskStatusChangedDomainEvent>
{
    public TaskStatusChangedNotificationHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<TaskStatusChangedNotificationHandler> logger)
        : base(context, currentUser, dateTimeProvider, unitOfWork, logger)
    {
    }

    protected override async Task<IReadOnlyCollection<NotificationRequest>> ResolveAsync(
        TaskStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var task = await Context.ProjectTasks
            .AsNoTracking()
            .Where(task => task.Id == domainEvent.TaskId)
            .Select(task => new { task.AssigneeId, Title = task.Title.Value })
            .SingleOrDefaultAsync(cancellationToken);

        if (task?.AssigneeId is not { } assigneeId)
        {
            return [];
        }

        return
        [
            new NotificationRequest(
                assigneeId,
                NotificationType.TaskStatusChanged,
                $"Task \"{task.Title}\" moved from {domainEvent.OldStatus} to {domainEvent.NewStatus}."),
        ];
    }
}
