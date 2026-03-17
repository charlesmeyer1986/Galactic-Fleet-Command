namespace GalacticFleetCommand.Api.Domain.Models;

public class ResourcePool : IVersionedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Version { get; set; } = 1;
    public ResourceType ResourceType { get; set; }
    public int Total { get; set; }
    public int Reserved { get; set; }
    public int Available => Total - Reserved;
}
