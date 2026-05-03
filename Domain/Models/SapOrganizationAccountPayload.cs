using System.Text.Json;

namespace Domain.Models;

// Flexible source payload holder; SAP shape can evolve by business unit.
public sealed class SapOrganizationAccountPayload
{
    public Dictionary<string, JsonElement> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
