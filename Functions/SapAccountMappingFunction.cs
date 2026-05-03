using System.Text.Json;
using Application.Services;
using Domain.Models;
using Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

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
        var validation = _validator.ValidateAndNormalize(aiJson, sapMessage, out var dynamicsPayload, out _);

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

    [Function("SapAccountMappingHttp")]
    public async Task<HttpResponseData> RunHttpAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData request,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var requestBody = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);

        SapServiceBusMessage sapMessage;
        try
        {
            sapMessage = DeserializeMessage(requestBody);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP mapping request was invalid. mappingStatus=DeserializationFailed");
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Invalid payload. Ensure organizationAccount exists and JSON is valid.", cancellationToken);
            return badRequest;
        }

        var correlationId = sapMessage.CorrelationId ?? "unknown";
        var eventType = sapMessage.EventType ?? "unknown";

        _logger.LogInformation("HTTP mapping started. correlationId={correlationId}, eventType={eventType}, sourceSystem={sourceSystem}, mappingStatus=Started",
            correlationId, eventType, sapMessage.SourceSystem ?? "unknown");

        try
        {
            var aiJson = await _openAiMappingService.MapSapToDynamicsJsonAsync(sapMessage, cancellationToken);
            var validation = _validator.ValidateAndNormalize(aiJson, sapMessage, out var dynamicsPayload, out _);

            if (!validation.IsValid || dynamicsPayload is null)
            {
                var unprocessable = request.CreateResponse((HttpStatusCode)422);
                await unprocessable.WriteAsJsonAsync(new
                {
                    message = "Dynamics payload validation failed.",
                    correlationId,
                    eventType,
                    validationErrors = validation.Errors
                }, cancellationToken);
                return unprocessable;
            }

            _payloadLoggingService.LogFinalPayload(correlationId, eventType, dynamicsPayload);
            _logger.LogInformation("HTTP mapping completed. correlationId={correlationId}, eventType={eventType}, mappingStatus=Succeeded",
                correlationId, eventType);

            var ok = request.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(new
            {
                message = "Mapping completed successfully.",
                correlationId,
                eventType,
                dynamicsPayload
            }, cancellationToken);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP mapping failed. correlationId={correlationId}, eventType={eventType}, mappingStatus=Failed",
                correlationId, eventType);
            var failed = request.CreateResponse(HttpStatusCode.InternalServerError);
            await failed.WriteStringAsync("Mapping failed due to an internal error.", cancellationToken);
            return failed;
        }
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
