using System.Collections.Concurrent;

namespace GalacticFleetCommand.Api.Services;

public class CommandQueue : ICommandQueue
{
    private readonly ConcurrentQueue<Guid> _queue = new();

    public void Enqueue(Guid commandId) => _queue.Enqueue(commandId);

    public bool TryDequeue(out Guid commandId) => _queue.TryDequeue(out commandId);

    public int Count => _queue.Count;
}
