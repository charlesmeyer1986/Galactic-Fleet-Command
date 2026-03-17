using System.Collections.Concurrent;
using GalacticFleetCommand.Api.Domain.Events;
using GalacticFleetCommand.Api.Domain.Exceptions;
using GalacticFleetCommand.Api.Domain.Models;
using GalacticFleetCommand.Api.Persistence;

namespace GalacticFleetCommand.Api.Services;

public class ResourceService : IResourceService
{
    private const int MaxRetries = 3;

    private static readonly Dictionary<ResourceType, int> DefaultTotals = new()
    {
        [ResourceType.Fuel] = 1000,
        [ResourceType.HyperdriveCore] = 100,
        [ResourceType.BattleDroids] = 500
    };

    private readonly IResourcePoolRepository _repo;
    private readonly IEventBus _eventBus;
    private readonly ConcurrentDictionary<Guid, List<ResourceRequirement>> _holdings = new();

    public ResourceService(IResourcePoolRepository repo, IEventBus eventBus)
    {
        _repo = repo;
        _eventBus = eventBus;
    }

    public async Task SeedPools()
    {
        foreach (var (type, total) in DefaultTotals)
        {
            var existing = await _repo.GetByType(type);
            if (existing is null)
            {
                await _repo.Create(new ResourcePool
                {
                    ResourceType = type,
                    Total = total,
                    Reserved = 0
                });
            }
        }
    }

    public async Task Reserve(Guid fleetId, List<ResourceRequirement> requirements)
    {
        for (int retry = 0; retry < MaxRetries; retry++)
        {
            var reserved = new List<(ResourcePool pool, int qty)>();
            try
            {
                foreach (var req in requirements)
                {
                    var pool = await _repo.GetByType(req.Type)
                        ?? throw new NotFoundException("ResourcePool", Guid.Empty);

                    await _repo.Update(pool.Id, pool.Version, p =>
                    {
                        if (p.Reserved + req.Quantity > p.Total)
                            throw new InsufficientResourceException(req.Type, req.Quantity, p.Available);
                        p.Reserved += req.Quantity;
                    });

                    // Re-read to get updated version for potential rollback
                    var updatedPool = (await _repo.Get(pool.Id))!;
                    reserved.Add((updatedPool, req.Quantity));
                }

                _holdings[fleetId] = [.. requirements];

                _eventBus.Publish(new FleetEvent(
                    Guid.NewGuid(), fleetId, FleetEventType.ResourcesReserved, DateTime.UtcNow,
                    new Dictionary<string, object>
                    {
                        ["resources"] = requirements.Select(r => new { r.Type, r.Quantity }).ToList()
                    }));

                return; // success
            }
            catch (ConcurrencyException) when (retry < MaxRetries - 1)
            {
                // Rollback what was reserved in this attempt, then retry
                await RollbackReserved(reserved);
            }
            catch (InsufficientResourceException)
            {
                await RollbackReserved(reserved);
                throw;
            }
            catch (Exception)
            {
                await RollbackReserved(reserved);
                throw;
            }
        }
    }

    public async Task Release(Guid fleetId)
    {
        if (!_holdings.TryRemove(fleetId, out var requirements))
            return;

        foreach (var req in requirements)
        {
            var pool = await _repo.GetByType(req.Type);
            if (pool is null) continue;

            // Retry release in case of concurrency conflict
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                try
                {
                    pool = (await _repo.GetByType(req.Type))!;
                    await _repo.Update(pool.Id, pool.Version, p =>
                    {
                        p.Reserved = Math.Max(0, p.Reserved - req.Quantity);
                    });
                    break;
                }
                catch (ConcurrencyException) when (retry < MaxRetries - 1)
                {
                    // retry
                }
            }
        }

        _eventBus.Publish(new FleetEvent(
            Guid.NewGuid(), fleetId, FleetEventType.ResourcesReleased, DateTime.UtcNow));
    }

    public async Task<int> GetAvailability(ResourceType type)
    {
        var pool = await _repo.GetByType(type);
        return pool?.Available ?? 0;
    }

    private async Task RollbackReserved(List<(ResourcePool pool, int qty)> reserved)
    {
        foreach (var (pool, qty) in reserved)
        {
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                try
                {
                    var current = (await _repo.Get(pool.Id))!;
                    await _repo.Update(current.Id, current.Version, p =>
                    {
                        p.Reserved = Math.Max(0, p.Reserved - qty);
                    });
                    break;
                }
                catch (ConcurrencyException) when (retry < MaxRetries - 1)
                {
                    // retry
                }
            }
        }
    }
}
