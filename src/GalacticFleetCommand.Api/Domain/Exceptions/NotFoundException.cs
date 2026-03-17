namespace GalacticFleetCommand.Api.Domain.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityType, Guid id)
        : base($"{entityType} with ID '{id}' was not found.") { }
}
