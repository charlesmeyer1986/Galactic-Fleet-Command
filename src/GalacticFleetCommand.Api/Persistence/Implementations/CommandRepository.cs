using GalacticFleetCommand.Api.Domain.Models;

namespace GalacticFleetCommand.Api.Persistence.Implementations;

public class CommandRepository : InMemoryRepository<Command>, ICommandRepository
{
    public Task<Command?> GetByIdempotencyKey(string key)
    {
        var command = Store.Values.FirstOrDefault(c => c.IdempotencyKey == key);
        return Task.FromResult(command);
    }
}
