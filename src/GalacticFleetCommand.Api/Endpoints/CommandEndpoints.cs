using System.Text.Json;
using GalacticFleetCommand.Api.Domain.Models;
using GalacticFleetCommand.Api.Persistence;
using GalacticFleetCommand.Api.Services;

namespace GalacticFleetCommand.Api.Endpoints;

public static class CommandEndpoints
{
    public static void MapCommandEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/commands");

        group.MapPost("/", CreateCommand);
        group.MapGet("/{id:guid}", GetCommand);
    }

    private static async Task<IResult> CreateCommand(
        CreateCommandRequest request,
        ICommandRepository repo,
        ICommandQueue queue)
    {
        // Idempotency check
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existing = await repo.GetByIdempotencyKey(request.IdempotencyKey);
            if (existing is not null)
            {
                return existing.Status switch
                {
                    CommandStatus.Succeeded => Results.Ok(existing),
                    CommandStatus.Queued or CommandStatus.Processing => Results.Accepted($"/commands/{existing.Id}", existing),
                    // Failed — allow re-submission below
                    _ => CreateAndEnqueue(request, repo, queue).Result
                };
            }
        }

        return await CreateAndEnqueue(request, repo, queue);
    }

    private static async Task<IResult> CreateAndEnqueue(
        CreateCommandRequest request,
        ICommandRepository repo,
        ICommandQueue queue)
    {
        var command = new Command
        {
            CommandType = request.Type,
            Payload = request.Payload,
            IdempotencyKey = request.IdempotencyKey ?? $"{request.Type}:{Guid.NewGuid()}",
            Status = CommandStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repo.Create(command);
        queue.Enqueue(command.Id);

        return Results.Accepted($"/commands/{command.Id}", command);
    }

    private static async Task<IResult> GetCommand(Guid id, ICommandRepository repo)
    {
        var command = await repo.Get(id);
        return command is null
            ? Results.NotFound(new { error = $"Command '{id}' not found." })
            : Results.Ok(command);
    }
}

public record CreateCommandRequest(
    string Type,
    JsonElement? Payload = null,
    string? IdempotencyKey = null);
