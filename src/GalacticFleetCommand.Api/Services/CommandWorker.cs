using GalacticFleetCommand.Api.Domain.Models;
using GalacticFleetCommand.Api.Persistence;

namespace GalacticFleetCommand.Api.Services;

public class CommandWorker : BackgroundService
{
    private const int MaxRetries = 3;
    private const int PollDelayMs = 50;

    private readonly ICommandQueue _queue;
    private readonly ICommandRepository _commandRepo;
    private readonly IEnumerable<ICommandHandler> _handlers;
    private readonly ILogger<CommandWorker> _logger;

    public CommandWorker(
        ICommandQueue queue,
        ICommandRepository commandRepo,
        IEnumerable<ICommandHandler> handlers,
        ILogger<CommandWorker> logger)
    {
        _queue = queue;
        _commandRepo = commandRepo;
        _handlers = handlers;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await ProcessOneAsync();
            if (!processed)
            {
                await Task.Delay(PollDelayMs, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Process a single command from the queue. Returns true if a command was processed.
    /// Public for deterministic testing.
    /// </summary>
    public async Task<bool> ProcessOneAsync()
    {
        if (!_queue.TryDequeue(out var commandId))
            return false;

        var command = await _commandRepo.Get(commandId);
        if (command is null)
        {
            _logger.LogWarning("Command {CommandId} not found in repository.", commandId);
            return true;
        }

        // Skip if already succeeded (idempotency)
        if (command.Status == CommandStatus.Succeeded)
            return true;

        var handler = _handlers.FirstOrDefault(h => h.CommandType == command.CommandType);
        if (handler is null)
        {
            _logger.LogError("No handler registered for command type '{CommandType}'.", command.CommandType);
            await _commandRepo.Update(command.Id, command.Version, c =>
            {
                c.Status = CommandStatus.Failed;
                c.UpdatedAt = DateTime.UtcNow;
                c.Attempts.Add(new CommandAttempt(
                    c.Attempts.Count + 1, DateTime.UtcNow, DateTime.UtcNow,
                    $"No handler registered for command type '{c.CommandType}'."));
            });
            return true;
        }

        var attemptNumber = command.Attempts.Count + 1;
        var startedAt = DateTime.UtcNow;

        try
        {
            await _commandRepo.Update(command.Id, command.Version, c =>
            {
                c.Status = CommandStatus.Processing;
                c.UpdatedAt = DateTime.UtcNow;
            });

            // Re-read for latest version
            command = (await _commandRepo.Get(commandId))!;

            await handler.Execute(command);

            await _commandRepo.Update(command.Id, command.Version, c =>
            {
                c.Status = CommandStatus.Succeeded;
                c.UpdatedAt = DateTime.UtcNow;
                c.Result = command.Result;
                c.Attempts.Add(new CommandAttempt(attemptNumber, startedAt, DateTime.UtcNow, null));
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {CommandId} attempt {Attempt} failed.", commandId, attemptNumber);

            command = (await _commandRepo.Get(commandId))!;
            var errorMessage = ex.Message;

            await _commandRepo.Update(command.Id, command.Version, c =>
            {
                c.Attempts.Add(new CommandAttempt(attemptNumber, startedAt, DateTime.UtcNow, errorMessage));
                c.UpdatedAt = DateTime.UtcNow;

                if (c.Attempts.Count < MaxRetries)
                {
                    c.Status = CommandStatus.Queued;
                }
                else
                {
                    c.Status = CommandStatus.Failed;
                }
            });

            // Re-enqueue if retries remain
            command = (await _commandRepo.Get(commandId))!;
            if (command.Status == CommandStatus.Queued)
            {
                _queue.Enqueue(commandId);
            }
        }

        return true;
    }
}
