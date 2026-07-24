namespace ProjectHub.Domain.Abstractions;

public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}