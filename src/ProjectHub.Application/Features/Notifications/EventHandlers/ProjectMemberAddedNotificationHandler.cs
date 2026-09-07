using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;

namespace ProjectHub.Application.Features.Notifications.EventHandlers;

/// <summary>
/// Notifies a user when they are added to a project, so they learn of the new membership and their role.
/// When a project is created its owner is added as the first member; because the owner is the actor there,
/// the base's actor exclusion prevents a spurious "you were added to your own project" notification.
/// </summary>
public sealed class ProjectMemberAddedNotificationHandler
    : NotificationEventHandler<ProjectMemberAddedDomainEvent>
{
    public ProjectMemberAddedNotificationHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<ProjectMemberAddedNotificationHandler> logger)
        : base(context, currentUser, dateTimeProvider, unitOfWork, logger)
    {
    }

    protected override async Task<IReadOnlyCollection<NotificationRequest>> ResolveAsync(
        ProjectMemberAddedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var projectName = await Context.Projects
            .AsNoTracking()
            .Where(project => project.Id == domainEvent.ProjectId)
            .Select(project => project.Name.Value)
            .SingleOrDefaultAsync(cancellationToken);

        if (projectName is null)
        {
            return [];
        }

        return
        [
            new NotificationRequest(
                domainEvent.UserId,
                NotificationType.ProjectInvitation,
                $"You were added to the project \"{projectName}\" as {domainEvent.Role}."),
        ];
    }
}
