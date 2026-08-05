using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Projects.ArchiveProject;

/// <summary>
/// Command to archive a project. Archiving is our SOFT retirement: the project's rows survive (history,
/// tasks, comments stay auditable) but the aggregate becomes read-only, so it's a state TRANSITION, not
/// a delete. The <see cref="ProjectId"/> comes from the route; there is no body because archiving carries
/// no new values — the only inputs are "which project" (route) and "who is asking" (resolved from the
/// authenticated principal in the handler).
/// </summary>
/// <remarks>
/// WHY A DEDICATED COMMAND INSTEAD OF REUSING UPDATE WITH A STATUS FIELD?
/// Archiving is a distinct business intent with its own invariant ("cannot archive an already-archived
/// project"), its own domain event (<c>ProjectArchivedDomainEvent</c>), and its own authorization bar
/// (Owner-only). Task-based, intent-revealing commands beat a generic "set these fields" update: they
/// make the audit log meaningful, keep authorization per-operation, and let the domain raise the right
/// event. This is the CQRS "one command per use case" principle applied honestly.
/// </remarks>
public sealed record ArchiveProjectCommand(Guid ProjectId) : ICommand;
