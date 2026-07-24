using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record UserRoleAssignedDomainEvent(
    Guid UserId,
    Guid RoleId,
    DateTime OccurredAtUtc) : IDomainEvent;
