using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Tests.Entities;

public class CommentTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TaskId = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();

    private static Comment CreateComment(string text = "Looks good to me") =>
        Comment.Create(TaskId, AuthorId, CommentBody.Create(text), UtcNow);

    [Fact]
    public void Create_ShouldReturnComment_AndRaiseEvent()
    {
        var comment = CreateComment();

        Assert.NotEqual(Guid.Empty, comment.Id);
        Assert.Equal(TaskId, comment.TaskId);
        Assert.Equal(AuthorId, comment.AuthorId);
        Assert.Equal("Looks good to me", comment.Body.Value);
        Assert.False(comment.IsEdited);
        Assert.Contains(comment.DomainEvents, e => e is CommentAddedDomainEvent);
    }

    [Fact]
    public void Edit_ShouldUpdateBody_SetEditedFlag_AndRaiseEvent()
    {
        var comment = CreateComment();
        comment.ClearDomainEvents();

        comment.Edit(CommentBody.Create("Updated text"), AuthorId, UtcNow);

        Assert.Equal("Updated text", comment.Body.Value);
        Assert.True(comment.IsEdited);
        Assert.Contains(comment.DomainEvents, e => e is CommentEditedDomainEvent);
    }

    [Fact]
    public void Edit_ShouldThrow_WhenEditorIsNotAuthor()
    {
        var comment = CreateComment();
        var otherUser = Guid.NewGuid();

        Assert.Throws<DomainException>(() =>
            comment.Edit(CommentBody.Create("Hacked"), otherUser, UtcNow));
    }

    [Fact]
    public void CommentBody_ShouldThrow_WhenEmpty()
    {
        Assert.Throws<ArgumentException>(() => CommentBody.Create("  "));
    }

    [Fact]
    public void CommentBody_ShouldThrow_WhenTooLong()
    {
        var tooLong = new string('x', 2001);

        Assert.Throws<DomainException>(() => CommentBody.Create(tooLong));
    }
}
