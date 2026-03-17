using System.Text.Json;
using GalacticFleetCommand.Api.Domain.Events;
using GalacticFleetCommand.Api.Domain.Models;
using GalacticFleetCommand.Api.Persistence.Implementations;
using GalacticFleetCommand.Api.Services;
using GalacticFleetCommand.Api.Services.Handlers;
using Microsoft.Extensions.Logging.Abstractions;

namespace GalacticFleetCommand.Tests.Services;

public class CommandProcessingTests
{
    private readonly FleetRepository _fleetRepo;
    private readonly CommandRepository _commandRepo;
    private readonly ResourcePoolRepository _poolRepo;
    private readonly EventBus _eventBus;
    private readonly ResourceService _resourceService;
    private readonly CommandQueue _queue;
    private readonly CommandWorker _worker;

    public CommandProcessingTests()
    {
        _fleetRepo = new FleetRepository();
        _commandRepo = new CommandRepository();
        _poolRepo = new ResourcePoolRepository();
        _eventBus = new EventBus();
        _resourceService = new ResourceService(_poolRepo, _eventBus);
        _resourceService.SeedPools().GetAwaiter().GetResult();
        _queue = new CommandQueue();

        var handlers = new ICommandHandler[]
        {
            new PrepareFleetHandler(_fleetRepo, _resourceService, _eventBus),
            new DeployFleetHandler(_fleetRepo, _eventBus)
        };

        _worker = new CommandWorker(_queue, _commandRepo, handlers, NullLogger<CommandWorker>.Instance);
    }

    [Fact]
    public async Task Command_FullLifecycle_QueuedToSucceeded()
    {
        var fleet = new Fleet
        {
            Name = "TestFleet",
            ResourceRequirements = [new ResourceRequirement(ResourceType.Fuel, 10)]
        };
        await _fleetRepo.Create(fleet);

        var payload = JsonSerializer.SerializeToElement(new { fleetId = fleet.Id });
        var command = new Command
        {
            CommandType = "PrepareFleet",
            Payload = payload,
            IdempotencyKey = $"PrepareFleet:{fleet.Id}:test"
        };
        await _commandRepo.Create(command);
        _queue.Enqueue(command.Id);

        Assert.Equal(CommandStatus.Queued, command.Status);

        await _worker.ProcessOneAsync();

        var processed = await _commandRepo.GetOrThrow(command.Id);
        Assert.Equal(CommandStatus.Succeeded, processed.Status);
        Assert.Single(processed.Attempts);
        Assert.Null(processed.Attempts[0].Error);

        var updatedFleet = await _fleetRepo.GetOrThrow(fleet.Id);
        Assert.Equal(FleetState.Ready, updatedFleet.State);
    }

    [Fact]
    public async Task FailedCommand_RecordsErrorAndAttempt()
    {
        // Create a command targeting a non-existent fleet
        var payload = JsonSerializer.SerializeToElement(new { fleetId = Guid.NewGuid() });
        var command = new Command
        {
            CommandType = "PrepareFleet",
            Payload = payload,
            IdempotencyKey = $"PrepareFleet:nonexistent:test"
        };
        await _commandRepo.Create(command);
        _queue.Enqueue(command.Id);

        // Process all retries
        for (int i = 0; i < 3; i++)
            await _worker.ProcessOneAsync();

        var processed = await _commandRepo.GetOrThrow(command.Id);
        Assert.Equal(CommandStatus.Failed, processed.Status);
        Assert.Equal(3, processed.Attempts.Count);
        Assert.All(processed.Attempts, a => Assert.NotNull(a.Error));
    }

    [Fact]
    public async Task Idempotency_PrepareFleet_NoDoubleReservation()
    {
        var fleet = new Fleet
        {
            Name = "IdempotentFleet",
            ResourceRequirements = [new ResourceRequirement(ResourceType.Fuel, 50)]
        };
        await _fleetRepo.Create(fleet);

        var payload = JsonSerializer.SerializeToElement(new { fleetId = fleet.Id });

        // First command
        var cmd1 = new Command
        {
            CommandType = "PrepareFleet",
            Payload = payload,
            IdempotencyKey = $"PrepareFleet:{fleet.Id}:idem1"
        };
        await _commandRepo.Create(cmd1);
        _queue.Enqueue(cmd1.Id);
        await _worker.ProcessOneAsync();

        var fuelAfterFirst = await _resourceService.GetAvailability(ResourceType.Fuel);

        // Second command for the same fleet — should be idempotent (fleet already Ready)
        var cmd2 = new Command
        {
            CommandType = "PrepareFleet",
            Payload = payload,
            IdempotencyKey = $"PrepareFleet:{fleet.Id}:idem2"
        };
        await _commandRepo.Create(cmd2);
        _queue.Enqueue(cmd2.Id);
        await _worker.ProcessOneAsync();

        var fuelAfterSecond = await _resourceService.GetAvailability(ResourceType.Fuel);

        // Fuel should not have been double-deducted
        Assert.Equal(fuelAfterFirst, fuelAfterSecond);

        var processed2 = await _commandRepo.GetOrThrow(cmd2.Id);
        Assert.Equal(CommandStatus.Succeeded, processed2.Status);
    }

    [Fact]
    public async Task DeployFleet_FullFlow()
    {
        var fleet = new Fleet
        {
            Name = "DeployableFleet",
            ResourceRequirements = [new ResourceRequirement(ResourceType.BattleDroids, 10)]
        };
        await _fleetRepo.Create(fleet);

        // Prepare first
        var prepPayload = JsonSerializer.SerializeToElement(new { fleetId = fleet.Id });
        var prepCmd = new Command
        {
            CommandType = "PrepareFleet",
            Payload = prepPayload,
            IdempotencyKey = $"PrepareFleet:{fleet.Id}:deploy-test"
        };
        await _commandRepo.Create(prepCmd);
        _queue.Enqueue(prepCmd.Id);
        await _worker.ProcessOneAsync();

        var readyFleet = await _fleetRepo.GetOrThrow(fleet.Id);
        Assert.Equal(FleetState.Ready, readyFleet.State);

        // Deploy
        var deployPayload = JsonSerializer.SerializeToElement(new { fleetId = fleet.Id });
        var deployCmd = new Command
        {
            CommandType = "DeployFleet",
            Payload = deployPayload,
            IdempotencyKey = $"DeployFleet:{fleet.Id}:deploy-test"
        };
        await _commandRepo.Create(deployCmd);
        _queue.Enqueue(deployCmd.Id);
        await _worker.ProcessOneAsync();

        var deployed = await _fleetRepo.GetOrThrow(fleet.Id);
        Assert.Equal(FleetState.Deployed, deployed.State);

        var processedDeploy = await _commandRepo.GetOrThrow(deployCmd.Id);
        Assert.Equal(CommandStatus.Succeeded, processedDeploy.Status);
    }

    [Fact]
    public async Task UnknownCommandType_FailsImmediately()
    {
        var command = new Command
        {
            CommandType = "UnknownType",
            IdempotencyKey = "unknown:test"
        };
        await _commandRepo.Create(command);
        _queue.Enqueue(command.Id);
        await _worker.ProcessOneAsync();

        var processed = await _commandRepo.GetOrThrow(command.Id);
        Assert.Equal(CommandStatus.Failed, processed.Status);
        Assert.Single(processed.Attempts);
        Assert.Contains("No handler registered", processed.Attempts[0].Error);
    }
}
