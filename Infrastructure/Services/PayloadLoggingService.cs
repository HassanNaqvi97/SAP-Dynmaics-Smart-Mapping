using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public interface IPayloadLoggingService
{
    void LogFinalPayload(string correlationId, string eventType, DynamicsAccountPayload payload);
}

public sealed class PayloadLoggingService : IPayloadLoggingService
{
    private readonly ILogger<PayloadLoggingService> _logger;

    public PayloadLoggingService(ILogger<PayloadLoggingService> logger)
    {
        _logger = logger;
    }

    public void LogFinalPayload(string correlationId, string eventType, DynamicsAccountPayload payload)
    {
        _logger.LogInformation("Final Dynamics payload generated. {correlationId} {eventType} {@payload}", correlationId, eventType, payload);
    }
}
