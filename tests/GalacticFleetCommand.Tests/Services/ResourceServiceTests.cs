using GalacticFleetCommand.Api.Domain.Events;
using GalacticFleetCommand.Api.Domain.Exceptions;
using GalacticFleetCommand.Api.Domain.Models;
using GalacticFleetCommand.Api.Persistence.Implementations;
using GalacticFleetCommand.Api.Services;

namespace GalacticFleetCommand.Tests.Services;

public class ResourceServiceTests
{
    private readonly ResourcePoolRepository _poolRepo;
    private readonly EventBus _eventBus;
    private readonly ResourceService _service;

    public ResourceServiceTests()
    {
        _poolRepo = new ResourcePoolRepository();
        _eventBus = new EventBus();
        _service = new ResourceService(_poolRepo, _eventBus);
        _service.SeedPools().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Reserve_SuccessfullyReserves_Resources()
    {
        var fleetId = Guid.NewGuid();
        var requirements = new List<ResourceRequirement>
        {
            new(ResourceType.Fuel, 100),
            new(ResourceType.HyperdriveCore, 10)
        };

        await _service.Reserve(fleetId, requirements);

        Assert.Equal(900, await _service.GetAvailability(ResourceType.Fuel));
        Assert.Equal(90, await _service.GetAvailability(ResourceType.HyperdriveCore));
    }

    [Fact]
    public async Task Reserve_ThrowsInsufficientResource_WhenNotEnough()
    {
        var fleetId = Guid.NewGuid();
        var requirements = new List<ResourceRequirement>
        {
            new(ResourceType.HyperdriveCore, 999) // only 100 available
        };

        await Assert.ThrowsAsync<InsufficientResourceException>(
            () => _service.Reserve(fleetId, requirements));
    }

    [Fact]
    public async Task Reserve_RollsBack_OnPartialFailure()
    {
        var fleetId = Guid.NewGuid();
        var requirements = new List<ResourceRequirement>
        {
            new(ResourceType.Fuel, 100),      // should succeed
            new(ResourceType.HyperdriveCore, 999) // should fail
        };

        await Assert.ThrowsAsync<InsufficientResourceException>(
            () => _service.Reserve(fleetId, requirements));

        // Fuel should be rolled back
        Assert.Equal(1000, await _service.GetAvailability(ResourceType.Fuel));
        Assert.Equal(100, await _service.GetAvailability(ResourceType.HyperdriveCore));
    }

    [Fact]
    public async Task Release_RestoresAvailability()
    {
        var fleetId = Guid.NewGuid();
        var requirements = new List<ResourceRequirement>
        {
            new(ResourceType.Fuel, 200),
            new(ResourceType.BattleDroids, 50)
        };

        await _service.Reserve(fleetId, requirements);
        Assert.Equal(800, await _service.GetAvailability(ResourceType.Fuel));

        await _service.Release(fleetId);
        Assert.Equal(1000, await _service.GetAvailability(ResourceType.Fuel));
        Assert.Equal(500, await _service.GetAvailability(ResourceType.BattleDroids));
    }

    [Fact]
    public async Task ConcurrentReserve_NoOverAllocation()
    {
        // All 100 hyperdrive cores are available.
        // Launch 10 fleets each trying to reserve 20 — only 5 should succeed.
        var tasks = new List<Task<(Guid fleetId, bool success)>>();

        for (int i = 0; i < 10; i++)
        {
            var fleetId = Guid.NewGuid();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _service.Reserve(fleetId, [new ResourceRequirement(ResourceType.HyperdriveCore, 20)]);
                    return (fleetId, true);
                }
                catch
                {
                    return (fleetId, false);
                }
            }));
        }

        var results = await Task.WhenAll(tasks);
        var successes = results.Count(r => r.success);
        var availability = await _service.GetAvailability(ResourceType.HyperdriveCore);

        // At most 5 can succeed (5 * 20 = 100)
        Assert.True(successes <= 5, $"Expected at most 5 successes, got {successes}");
        Assert.True(availability >= 0, $"Available should be >= 0, got {availability}");

        // Verify total reserved does not exceed total
        var pool = await _poolRepo.GetByType(ResourceType.HyperdriveCore);
        Assert.NotNull(pool);
        Assert.True(pool!.Reserved <= pool.Total,
            $"Over-allocation detected: reserved {pool.Reserved} > total {pool.Total}");
    }
}
