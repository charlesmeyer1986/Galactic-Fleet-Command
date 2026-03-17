using GalacticFleetCommand.Api.Domain.Models;

namespace GalacticFleetCommand.Api.Persistence;

public interface IResourcePoolRepository : IRepository<ResourcePool>
{
    Task<ResourcePool?> GetByType(ResourceType type);
}
