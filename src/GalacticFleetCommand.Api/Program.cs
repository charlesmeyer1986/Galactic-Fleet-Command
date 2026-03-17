using System.Text.Json;
using System.Text.Json.Serialization;
using GalacticFleetCommand.Api.Cache;
using GalacticFleetCommand.Api.Domain.Events;
using GalacticFleetCommand.Api.Domain.Exceptions;
using GalacticFleetCommand.Api.Domain.Models;
using GalacticFleetCommand.Api.Endpoints;
using GalacticFleetCommand.Api.Persistence;
using GalacticFleetCommand.Api.Persistence.Implementations;
using GalacticFleetCommand.Api.Services;
using GalacticFleetCommand.Api.Services.Handlers;

var builder = WebApplication.CreateBuilder(args);

// JSON serialization: camelCase + enums as strings
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Persistence — singletons (in-memory stores)
builder.Services.AddSingleton<IFleetRepository, FleetRepository>();
builder.Services.AddSingleton<ICommandRepository, CommandRepository>();
builder.Services.AddSingleton<IResourcePoolRepository, ResourcePoolRepository>();

// Domain services
builder.Services.AddSingleton<IEventBus, EventBus>();
builder.Services.AddSingleton<IResourceService, ResourceService>();
builder.Services.AddSingleton<ICommandQueue, CommandQueue>();

// LRU Cache for fleet reads (capacity 100)
builder.Services.AddSingleton(new LRUCache<Guid, Fleet>(100));

// Command handlers
builder.Services.AddSingleton<ICommandHandler, PrepareFleetHandler>();
builder.Services.AddSingleton<ICommandHandler, DeployFleetHandler>();

// Background worker
builder.Services.AddHostedService<CommandWorker>();

var app = builder.Build();

// Seed resource pools on startup
using (var scope = app.Services.CreateScope())
{
    var resourceService = scope.ServiceProvider.GetRequiredService<IResourceService>();
    await resourceService.SeedPools();
}

// Error handling middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (NotFoundException ex)
    {
        context.Response.StatusCode = 404;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (InvalidTransitionException ex)
    {
        context.Response.StatusCode = 409;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (ConcurrencyException ex)
    {
        context.Response.StatusCode = 409;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (DuplicateIdException ex)
    {
        context.Response.StatusCode = 409;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (InsufficientResourceException ex)
    {
        context.Response.StatusCode = 422;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

// Map endpoints
app.MapHealthEndpoints();
app.MapFleetEndpoints();
app.MapCommandEndpoints();

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
