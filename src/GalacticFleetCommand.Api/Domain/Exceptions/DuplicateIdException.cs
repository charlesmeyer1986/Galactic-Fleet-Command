namespace GalacticFleetCommand.Api.Domain.Exceptions;

public class DuplicateIdException : Exception
{
    public DuplicateIdException(Guid id)
        : base($"An entity with ID '{id}' already exists.") { }
}
