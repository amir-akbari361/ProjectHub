using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Attachments.DeleteAttachment;

/// <summary>
/// Removes an attachment — both its metadata row AND the underlying bytes. A WRITE-side command that
/// returns nothing (<see cref="ICommand"/> without a payload): a delete is an acknowledgement, so a 204 No
/// Content is the honest HTTP result. The caller is resolved server-side from the JWT, never trusted from
/// the request body, so only the attachment id is carried here.
/// </summary>
public sealed record DeleteAttachmentCommand(Guid AttachmentId) : ICommand;
