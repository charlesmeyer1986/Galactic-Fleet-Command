using GalacticFleetCommand.Api.Cache;
using GalacticFleetCommand.Api.Domain;
using GalacticFleetCommand.Api.Domain.Events;
using GalacticFleetCommand.Api.Domain.Exceptions;
using GalacticFleetCommand.Api.Domain.Models;
using GalacticFleetCommand.Api.Persistence;

namespace GalacticFleetCommand.Api.Endpoints;

public static class FleetEndpoints
{
    public static void MapFleetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/fleets");

        group.MapPost("/", CreateFleet);
        group.MapPatch("/{id:guid}", UpdateFleet);
        group.MapGet("/{id:guid}", GetFleet);
        group.MapGet("/{id:guid}/timeline", GetTimeline);
    }

    private static async Task<IResult> CreateFleet(
        CreateFleetRequest request,
        IFleetRepository repo,
        IEventBus eventBus)
    {
        var fleet = new Fleet
        {
            Name = request.Name,
            Ships = request.Ships ?? [],
            Loadout = request.Loadout ?? [],
            ResourceRequirements = request.ResourceRequirements ?? []
        };

        await repo.Create(fleet);

        eventBus.Publish(new FleetEvent(
            Guid.NewGuid(), fleet.Id, FleetEventType.FleetCreated, DateTime.UtcNow,
            new Dictionary<string, object> { ["name"] = fleet.Name }));

        return Results.Created($"/fleets/{fleet.Id}", fleet);
    }

    private static async Task<IResult> UpdateFleet(
        Guid id,
        UpdateFleetRequest request,
        IFleetRepository repo,
        IEventBus eventBus,
        LRUCache<Guid, Fleet> cache)
    {
        var fleet = await repo.GetOrThrow(id);
        FleetStateMachine.AssertEditable(fleet.State);

        await repo.Update(id, request.Version, f =>
        {
            if (request.Ships is not null) f.Ships = request.Ships;
            if (request.Loadout is not null) f.Loadout = request.Loadout;
            if (request.ResourceRequirements is not null) f.ResourceRequirements = request.ResourceRequirements;
            if (request.Name is not null) f.Name = request.Name;
        });

        cache.Remove(id);

        eventBus.Publish(new FleetEvent(
            Guid.NewGuid(), id, FleetEventType.FleetModified, DateTime.UtcNow));

        fleet = await repo.GetOrThrow(id);
        return Results.Ok(fleet);
    }

    private static async Task<IResult> GetFleet(
        Guid id,
        IFleetRepository repo,
        LRUCache<Guid, Fleet> cache)
    {
        if (cache.TryGet(id, out var cached) && cached is not null)
            return Results.Ok(cached);

        var fleet = await repo.Get(id);
        if (fleet is null)
            return Results.NotFound(new { error = $"Fleet '{id}' not found." });

        cache.Put(id, fleet);
        return Results.Ok(fleet);
    }

    private static Task<IResult> GetTimeline(Guid id, IEventBus eventBus)
    {
        var timeline = eventBus.GetTimeline(id);
        return Task.FromResult(Results.Ok(timeline));
    }
}

public record CreateFleetRequest(
    string Name,
    List<Ship>? Ships = null,
    Dictionary<string, int>? Loadout = null,
    List<ResourceRequirement>? ResourceRequirements = null);

public record UpdateFleetRequest(
    int Version,
    string? Name = null,
    List<Ship>? Ships = null,
    Dictionary<string, int>? Loadout = null,
    List<ResourceRequirement>? ResourceRequirements = null);
