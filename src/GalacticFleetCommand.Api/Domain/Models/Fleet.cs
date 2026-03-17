namespace GalacticFleetCommand.Api.Domain.Models;

public class Fleet : IVersionedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Version { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public FleetState State { get; set; } = FleetState.Docked;
    public List<Ship> Ships { get; set; } = [];
    public Dictionary<string, int> Loadout { get; set; } = [];
    public List<ResourceRequirement> ResourceRequirements { get; set; } = [];
}
