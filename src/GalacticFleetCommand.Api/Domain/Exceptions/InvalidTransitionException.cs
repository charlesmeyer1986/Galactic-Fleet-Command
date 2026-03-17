using GalacticFleetCommand.Api.Domain.Models;

namespace GalacticFleetCommand.Api.Domain.Exceptions;

public class InvalidTransitionException : Exception
{
    public InvalidTransitionException(FleetState from, FleetState to)
        : base($"Invalid state transition from '{from}' to '{to}'.") { }

    public InvalidTransitionException(string message) : base(message) { }
}
