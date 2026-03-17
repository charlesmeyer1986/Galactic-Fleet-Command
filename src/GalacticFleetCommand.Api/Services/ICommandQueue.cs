namespace GalacticFleetCommand.Api.Services;

public interface ICommandQueue
{
    void Enqueue(Guid commandId);
    bool TryDequeue(out Guid commandId);
    int Count { get; }
}
