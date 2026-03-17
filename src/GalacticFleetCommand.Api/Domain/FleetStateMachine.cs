using GalacticFleetCommand.Api.Domain.Exceptions;
using GalacticFleetCommand.Api.Domain.Models;

namespace GalacticFleetCommand.Api.Domain;

public static class FleetStateMachine
{
    private static readonly Dictionary<FleetState, HashSet<FleetState>> Transitions = new()
    {
        [FleetState.Docked] = [FleetState.Preparing],
        [FleetState.Preparing] = [FleetState.Ready, FleetState.FailedPreparation],
        [FleetState.Ready] = [FleetState.Deployed],
        [FleetState.Deployed] = [FleetState.InBattle],
        [FleetState.InBattle] = [FleetState.Victorious, FleetState.Destroyed],
        [FleetState.Victorious] = [],
        [FleetState.Destroyed] = [],
        [FleetState.FailedPreparation] = [FleetState.Docked]
    };

    private static readonly HashSet<FleetState> TerminalStates =
        [FleetState.Victorious, FleetState.Destroyed];

    public static bool CanTransition(FleetState from, FleetState to)
    {
        return Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    public static void AssertTransition(FleetState from, FleetState to)
    {
        if (!CanTransition(from, to))
            throw new InvalidTransitionException(from, to);
    }

    public static bool IsEditable(FleetState state) => state == FleetState.Docked;

    public static void AssertEditable(FleetState state)
    {
        if (!IsEditable(state))
            throw new InvalidTransitionException(
                $"Fleet in state '{state}' is not editable. Only Docked fleets can be modified.");
    }

    public static bool IsTerminal(FleetState state) => TerminalStates.Contains(state);
}
