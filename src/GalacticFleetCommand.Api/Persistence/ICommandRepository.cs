using GalacticFleetCommand.Api.Domain.Models;

namespace GalacticFleetCommand.Api.Persistence;

public interface ICommandRepository : IRepository<Command>
{
    Task<Command?> GetByIdempotencyKey(string key);
}
