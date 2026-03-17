using GalacticFleetCommand.Api.Domain;
using GalacticFleetCommand.Api.Domain.Events;
using GalacticFleetCommand.Api.Domain.Models;
using GalacticFleetCommand.Api.Persistence;

namespace GalacticFleetCommand.Api.Services.Handlers;

public class PrepareFleetHandler : ICommandHandler
{
    public string CommandType => "PrepareFleet";

    private readonly IFleetRepository _fleetRepo;
    private readonly IResourceService _resourceService;
    private readonly IEventBus _eventBus;

    public PrepareFleetHandler(IFleetRepository fleetRepo, IResourceService resourceService, IEventBus eventBus)
    {
        _fleetRepo = fleetRepo;
        _resourceService = resourceService;
        _eventBus = eventBus;
    }

    public async Task Execute(Command command)
    {
        var fleetId = command.Payload?.GetProperty("fleetId").GetGuid()
            ?? throw new InvalidOperationException("PrepareFleet command requires a fleetId in payload.");

        var fleet = await _fleetRepo.GetOrThrow(fleetId);

        // Idempotency: if already Ready or beyond, this is a no-op
        if (fleet.State == FleetState.Ready || FleetStateMachine.IsTerminal(fleet.State))
        {
            command.Result = new { fleetId, state = fleet.State.ToString(), message = "Already prepared." };
            return;
        }

        // Transition Docked → Preparing
        FleetStateMachine.AssertTransition(fleet.State, FleetState.Preparing);
        await _fleetRepo.Update(fleet.Id, fleet.Version, f => f.State = FleetState.Preparing);

        _eventBus.Publish(new FleetEvent(
            Guid.NewGuid(), fleetId, FleetEventType.FleetStateChanged, DateTime.UtcNow,
            new Dictionary<string, object> { ["from"] = "Docked", ["to"] = "Preparing" }));

        // Reserve resources
        try
        {
            fleet = await _fleetRepo.GetOrThrow(fleetId); // re-read for latest version
            await _resourceService.Reserve(fleetId, fleet.ResourceRequirements);

            // Transition Preparing → Ready
            fleet = await _fleetRepo.GetOrThrow(fleetId);
            await _fleetRepo.Update(fleet.Id, fleet.Version, f => f.State = FleetState.Ready);

            _eventBus.Publish(new FleetEvent(
                Guid.NewGuid(), fleetId, FleetEventType.FleetStateChanged, DateTime.UtcNow,
                new Dictionary<string, object> { ["from"] = "Preparing", ["to"] = "Ready" }));

            command.Result = new { fleetId, state = FleetState.Ready.ToString() };
        }
        catch (Exception ex)
        {
            // Transition Preparing → FailedPreparation
            fleet = await _fleetRepo.GetOrThrow(fleetId);
            if (fleet.State == FleetState.Preparing)
            {
                await _fleetRepo.Update(fleet.Id, fleet.Version, f => f.State = FleetState.FailedPreparation);

                _eventBus.Publish(new FleetEvent(
                    Guid.NewGuid(), fleetId, FleetEventType.FleetPreparationFailed, DateTime.UtcNow,
                    new Dictionary<string, object> { ["error"] = ex.Message }));
            }

            await _resourceService.Release(fleetId);
            throw;
        }
    }
}
