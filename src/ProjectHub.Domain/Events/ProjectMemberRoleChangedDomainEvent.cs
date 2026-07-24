using ProjectHub.Domain.Abstractions;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Domain.Events;

public sealed record ProjectMemberRoleChangedDomainEvent(
    Guid ProjectId,
    Guid UserId,
    ProjectRole OldRole,
    ProjectRole NewRole,
    DateTime OccurredAtUtc) : IDomainEvent;
