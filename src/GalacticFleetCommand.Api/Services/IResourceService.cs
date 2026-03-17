using GalacticFleetCommand.Api.Domain.Models;

namespace GalacticFleetCommand.Api.Services;

public interface IResourceService
{
    Task Reserve(Guid fleetId, List<ResourceRequirement> requirements);
    Task Release(Guid fleetId);
    Task<int> GetAvailability(ResourceType type);
    Task SeedPools();
}
