namespace Domain.Models;

public sealed class SapServiceBusMessage
{
    public string? CorrelationId { get; set; }
    public string? EventType { get; set; }
    public string? SourceSystem { get; set; }
    public SapOrganizationAccountPayload? OrganizationAccount { get; set; }
}
