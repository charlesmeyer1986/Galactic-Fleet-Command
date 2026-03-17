namespace GalacticFleetCommand.Api.Domain.Events;

public record FleetEvent(
    Guid Id,
    Guid FleetId,
    FleetEventType Type,
    DateTime Timestamp,
    Dictionary<string, object>? Data = null);
