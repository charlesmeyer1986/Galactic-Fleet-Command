namespace GalacticFleetCommand.Api.Domain.Models;

public interface IVersionedEntity
{
    Guid Id { get; }
    int Version { get; set; }
}
