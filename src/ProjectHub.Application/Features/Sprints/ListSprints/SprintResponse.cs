using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Sprints.ListSprints;

/// <summary>
/// Lean read model for a sprint returned by the list endpoint. Deliberately flat — the owned
/// <c>DateRange</c> schedule is projected into two primitive boundaries (<see cref="StartUtc"/> /
/// <see cref="EndUtc"/>) so the wire contract never depends on the domain value object, and the status
/// is the raw <see cref="SprintStatus"/> enum the UI maps to a chip. No aggregate is ever materialized to
/// build this; the handler projects straight into it.
/// </summary>
public sealed record SprintResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    DateTime StartUtc,
    DateTime EndUtc,
    SprintStatus Status);
