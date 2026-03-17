using System.Collections.Concurrent;
using GalacticFleetCommand.Api.Domain.Exceptions;
using GalacticFleetCommand.Api.Domain.Models;

namespace GalacticFleetCommand.Api.Persistence;

public class InMemoryRepository<T> : IRepository<T> where T : class, IVersionedEntity
{
    protected readonly ConcurrentDictionary<Guid, T> Store = new();
    private readonly ConcurrentDictionary<Guid, object> _locks = new();
    private readonly string _entityName = typeof(T).Name;

    private object GetLock(Guid id) => _locks.GetOrAdd(id, _ => new object());

    public Task<T> Create(T entity)
    {
        if (!Store.TryAdd(entity.Id, entity))
            throw new DuplicateIdException(entity.Id);

        return Task.FromResult(entity);
    }

    public Task<T?> Get(Guid id)
    {
        Store.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    public Task<T> GetOrThrow(Guid id)
    {
        if (!Store.TryGetValue(id, out var entity))
            throw new NotFoundException(_entityName, id);

        return Task.FromResult(entity);
    }

    public Task<T> Update(Guid id, int expectedVersion, Action<T> updater)
    {
        lock (GetLock(id))
        {
            if (!Store.TryGetValue(id, out var entity))
                throw new NotFoundException(_entityName, id);

            if (entity.Version != expectedVersion)
                throw new ConcurrencyException(id, expectedVersion, entity.Version);

            updater(entity);
            entity.Version++;

            return Task.FromResult(entity);
        }
    }

    public Task Delete(Guid id, int? expectedVersion = null)
    {
        lock (GetLock(id))
        {
            if (!Store.TryGetValue(id, out var entity))
                throw new NotFoundException(_entityName, id);

            if (expectedVersion.HasValue && entity.Version != expectedVersion.Value)
                throw new ConcurrencyException(id, expectedVersion.Value, entity.Version);

            Store.TryRemove(id, out _);
            _locks.TryRemove(id, out _);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        Store.Clear();
        _locks.Clear();
    }
}
