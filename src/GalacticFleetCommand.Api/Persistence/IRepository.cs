using GalacticFleetCommand.Api.Domain.Models;

namespace GalacticFleetCommand.Api.Persistence;

public interface IRepository<T> where T : class, IVersionedEntity
{
    Task<T> Create(T entity);
    Task<T?> Get(Guid id);
    Task<T> GetOrThrow(Guid id);
    Task<T> Update(Guid id, int expectedVersion, Action<T> updater);
    Task Delete(Guid id, int? expectedVersion = null);
    void Clear();
}
