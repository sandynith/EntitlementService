namespace EntitlementService.Core.Models;

/// <summary>
/// A protected resource in the system (e.g. an account, card, report).
/// </summary>
public record Resource(string ResourceId, string ResourceType);
