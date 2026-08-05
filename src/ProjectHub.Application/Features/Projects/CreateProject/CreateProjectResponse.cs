namespace ProjectHub.Application.Features.Projects.CreateProject;

/// <summary>
/// The payload returned after a successful project creation. We echo back the server-generated id
/// (the client needs it to navigate to the new resource) and the NORMALIZED name (trimmed by the
/// <c>ProjectName</c> value object) so the client sees exactly what was stored. Returning a dedicated
/// response record — rather than the full <c>Project</c> aggregate — keeps the API contract stable
/// and prevents leaking internal domain shape to the transport layer.
/// </summary>
public sealed record CreateProjectResponse(Guid Id, string Name);
