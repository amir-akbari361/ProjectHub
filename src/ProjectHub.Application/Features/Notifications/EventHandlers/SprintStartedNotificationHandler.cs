using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;

namespace ProjectHub.Application.Features.Notifications.EventHandlers;

/// <summary>
/// Notifies every member of a project when one of its sprints starts, so the whole team knows work has begun.
/// The member who started the sprint is one of those members and is dropped by the base's actor exclusion.
/// </summary>
public sealed class SprintStartedNotificationHandler : NotificationEventHandler<SprintStartedDomainEvent>
{
    public SprintStartedNotificationHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<SprintStartedNotificationHandler> logger)
        : base(context, currentUser, dateTimeProvider, unitOfWork, logger)
    {
    }

    protected override async Task<IReadOnlyCollection<NotificationRequest>> ResolveAsync(
        SprintStartedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var sprintName = await Context.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.Id == domainEvent.SprintId)
            .Select(sprint => sprint.Name)
            .SingleOrDefaultAsync(cancellationToken);

        if (sprintName is null)
        {
            return [];
        }

        // Fan out to the project's members. ProjectMember is not an aggregate root, so it is reached through
        // the Project.Members navigation rather than a top-level set.
        var memberIds = await Context.Projects
            .AsNoTracking()
            .Where(project => project.Id == domainEvent.ProjectId)
            .SelectMany(project => project.Members)
            .Select(member => member.UserId)
            .ToListAsync(cancellationToken);

        if (memberIds.Count == 0)
        {
            return [];
        }

        var message = $"Sprint \"{sprintName}\" has started.";
        return memberIds
            .Select(recipientId => new NotificationRequest(
                recipientId, NotificationType.SprintStarted, message))
            .ToList();
    }
}
