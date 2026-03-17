namespace GalacticFleetCommand.Api.Domain.Events;

public enum FleetEventType
{
    FleetCreated,
    FleetModified,
    FleetStateChanged,
    ResourcesReserved,
    ResourcesReleased,
    FleetPreparationFailed
}
