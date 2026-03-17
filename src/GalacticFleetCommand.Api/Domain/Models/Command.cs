using System.Text.Json;

namespace GalacticFleetCommand.Api.Domain.Models;

public class Command : IVersionedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Version { get; set; } = 1;
    public string CommandType { get; set; } = string.Empty;
    public CommandStatus Status { get; set; } = CommandStatus.Queued;
    public JsonElement? Payload { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public List<CommandAttempt> Attempts { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public object? Result { get; set; }
}
