using GalacticFleetCommand.Api.Domain.Models;

namespace GalacticFleetCommand.Api.Domain.Exceptions;

public class InsufficientResourceException : Exception
{
    public ResourceType ResourceType { get; }
    public int Requested { get; }
    public int Available { get; }

    public InsufficientResourceException(ResourceType resourceType, int requested, int available)
        : base($"Insufficient {resourceType}: requested {requested}, but only {available} available.")
    {
        ResourceType = resourceType;
        Requested = requested;
        Available = available;
    }
}
