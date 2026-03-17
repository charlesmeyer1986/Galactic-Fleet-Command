namespace GalacticFleetCommand.Api.Domain.Exceptions;

public class ConcurrencyException : Exception
{
    public Guid EntityId { get; }
    public int ExpectedVersion { get; }
    public int ActualVersion { get; }

    public ConcurrencyException(Guid entityId, int expectedVersion, int actualVersion)
        : base($"Concurrency conflict on entity '{entityId}': expected version {expectedVersion}, but found {actualVersion}.")
    {
        EntityId = entityId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}
