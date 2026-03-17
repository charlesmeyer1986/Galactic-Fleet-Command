using GalacticFleetCommand.Api.Domain.Models;

namespace GalacticFleetCommand.Api.Services;

public interface ICommandHandler
{
    string CommandType { get; }
    Task Execute(Command command);
}
