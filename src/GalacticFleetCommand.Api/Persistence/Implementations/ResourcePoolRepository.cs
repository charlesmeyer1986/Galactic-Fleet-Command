using GalacticFleetCommand.Api.Domain.Models;

namespace GalacticFleetCommand.Api.Persistence.Implementations;

public class ResourcePoolRepository : InMemoryRepository<ResourcePool>, IResourcePoolRepository
{
    public Task<ResourcePool?> GetByType(ResourceType type)
    {
        var pool = Store.Values.FirstOrDefault(p => p.ResourceType == type);
        return Task.FromResult(pool);
    }
}
