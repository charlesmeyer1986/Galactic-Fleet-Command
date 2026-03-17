using GalacticFleetCommand.Api.Domain;
using GalacticFleetCommand.Api.Domain.Events;
using GalacticFleetCommand.Api.Domain.Models;
using GalacticFleetCommand.Api.Persistence;

namespace GalacticFleetCommand.Api.Services.Handlers;

public class DeployFleetHandler : ICommandHandler
{
    public string CommandType => "DeployFleet";

    private readonly IFleetRepository _fleetRepo;
    private readonly IEventBus _eventBus;

    public DeployFleetHandler(IFleetRepository fleetRepo, IEventBus eventBus)
    {
        _fleetRepo = fleetRepo;
        _eventBus = eventBus;
    }

    public async Task Execute(Command command)
    {
        var fleetId = command.Payload?.GetProperty("fleetId").GetGuid()
            ?? throw new InvalidOperationException("DeployFleet command requires a fleetId in payload.");

        var fleet = await _fleetRepo.GetOrThrow(fleetId);

        // Idempotency: if already Deployed or beyond, this is a no-op
        if (fleet.State == FleetState.Deployed || fleet.State == FleetState.InBattle
            || FleetStateMachine.IsTerminal(fleet.State))
        {
            command.Result = new { fleetId, state = fleet.State.ToString(), message = "Already deployed." };
            return;
        }

        FleetStateMachine.AssertTransition(fleet.State, FleetState.Deployed);

        await _fleetRepo.Update(fleet.Id, fleet.Version, f => f.State = FleetState.Deployed);

        _eventBus.Publish(new FleetEvent(
            Guid.NewGuid(), fleetId, FleetEventType.FleetStateChanged, DateTime.UtcNow,
            new Dictionary<string, object> { ["from"] = "Ready", ["to"] = "Deployed" }));

        command.Result = new { fleetId, state = FleetState.Deployed.ToString() };
    }
}
