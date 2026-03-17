namespace GalacticFleetCommand.Api.Domain.Models;

public record CommandAttempt(int AttemptNumber, DateTime StartedAt, DateTime? CompletedAt, string? Error);
