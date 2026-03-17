namespace GalacticFleetCommand.Api.Domain.Models;

public enum FleetState
{
    Docked,
    Preparing,
    Ready,
    Deployed,
    InBattle,
    Victorious,
    Destroyed,
    FailedPreparation
}
