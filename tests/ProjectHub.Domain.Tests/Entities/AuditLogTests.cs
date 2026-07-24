using ProjectHub.Domain.Entities;

namespace ProjectHub.Domain.Tests.Entities;

public class AuditLogTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Record_ShouldCaptureAuditData()
    {
        var entityId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();

        var log = AuditLog.Record(
            "Project",
            entityId,
            "Archived",
            UtcNow,
            performedBy,
            "{\"Status\":\"Archived\"}");

        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal("Project", log.EntityName);
        Assert.Equal(entityId, log.EntityId);
        Assert.Equal("Archived", log.Action);
        Assert.Equal(performedBy, log.PerformedBy);
        Assert.Equal("{\"Status\":\"Archived\"}", log.Changes);
        Assert.Equal(UtcNow, log.CreatedAtUtc);
        Assert.Equal(performedBy, log.CreatedBy);
    }

    [Fact]
    public void Record_ShouldAllowNullPerformerAndChanges_ForSystemActions()
    {
        var log = AuditLog.Record("User", Guid.NewGuid(), "SystemPurge", UtcNow);

        Assert.Null(log.PerformedBy);
        Assert.Null(log.Changes);
    }

    [Fact]
    public void Record_ShouldThrow_WhenEntityNameIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            AuditLog.Record("  ", Guid.NewGuid(), "Created", UtcNow));
    }

    [Fact]
    public void Record_ShouldThrow_WhenActionIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            AuditLog.Record("Project", Guid.NewGuid(), " ", UtcNow));
    }

    [Fact]
    public void Record_ShouldThrow_WhenEntityIdIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            AuditLog.Record("Project", Guid.Empty, "Created", UtcNow));
    }
}
