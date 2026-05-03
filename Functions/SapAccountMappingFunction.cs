using System.Text.Json;
using Application.Services;
using Domain.Models;
using Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Functions;

public sealed class SapAccountMappingFunction
{
    private readonly IOpenAiMappingService _openAiMappingService;
    private readonly IDynamicsPayloadValidator _validator;
    private readonly IPayloadLoggingService _payloadLoggingService;
    private readonly ILogger<SapAccountMappingFunction> _logger;

    public SapAccountMappingFunction(
        IOpenAiMappingService openAiMappingService,
        IDynamicsPayloadValidator validator,
        IPayloadLoggingService payloadLoggingService,
        ILogger<SapAccountMappingFunction> logger)
    {
        _openAiMappingService = openAiMappingService;
        _validator = validator;
        _payloadLoggingService = payloadLoggingService;
        _logger = logger;
    }

    [Function(nameof(SapAccountMappingFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger("%ServiceBus__SapAccountQueueName%", Connection = "ServiceBusConnection")]
        string message,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        SapServiceBusMessage sapMessage;
        try
        {
            sapMessage = DeserializeMessage(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize Service Bus message. mappingStatus=DeserializationFailed");
            throw;
        }

        var correlationId = sapMessage.CorrelationId ?? "unknown";
        var eventType = sapMessage.EventType ?? "unknown";

        _logger.LogInformation("Mapping started. correlationId={correlationId}, eventType={eventType}, sourceSystem={sourceSystem}, mappingStatus=Started",
            correlationId, eventType, sapMessage.SourceSystem ?? "unknown");

        var aiJson = await _openAiMappingService.MapSapToDynamicsJsonAsync(sapMessage, cancellationToken);
        var validation = _validator.ValidateAndNormalize(aiJson, out var dynamicsPayload, out _);

        if (!validation.IsValid || dynamicsPayload is null)
        {
            _logger.LogError("Mapping validation failed. correlationId={correlationId}, eventType={eventType}, mappingStatus=ValidationFailed, validationErrors={validationErrors}",
                correlationId, eventType, string.Join(" | ", validation.Errors));
            throw new InvalidOperationException($"Dynamics payload validation failed: {string.Join("; ", validation.Errors)}");
        }

        _payloadLoggingService.LogFinalPayload(correlationId, eventType, dynamicsPayload);
        _logger.LogInformation("Mapping completed. correlationId={correlationId}, eventType={eventType}, mappingStatus=Succeeded", correlationId, eventType);

        // TODO: send to Dataverse via IDataverseService after integration contract is finalized.
    }

    private static SapServiceBusMessage DeserializeMessage(string message)
    {
        using var doc = JsonDocument.Parse(message);
        var root = doc.RootElement;

        if (!root.TryGetProperty("organizationAccount", out var orgElement))
            throw new InvalidOperationException("Message is missing organizationAccount.");

        var payload = new SapOrganizationAccountPayload();
        foreach (var prop in orgElement.EnumerateObject())
            payload.Fields[prop.Name] = prop.Value.Clone();

        return new SapServiceBusMessage
        {
            CorrelationId = root.TryGetProperty("correlationId", out var c) ? c.GetString() : null,
            EventType = root.TryGetProperty("eventType", out var e) ? e.GetString() : null,
            SourceSystem = root.TryGetProperty("sourceSystem", out var s) ? s.GetString() : null,
            OrganizationAccount = payload
        };
    }
}
