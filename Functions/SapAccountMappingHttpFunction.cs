using System.Net;
using System.Text.Json;
using Application.Services;
using Domain.Models;
using Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Functions;

public sealed class SapAccountMappingHttpFunction
{
    private readonly IOpenAiMappingService _openAiMappingService;
    private readonly IDynamicsPayloadValidator _validator;
    private readonly IPayloadLoggingService _payloadLoggingService;
    private readonly ILogger<SapAccountMappingHttpFunction> _logger;

    public SapAccountMappingHttpFunction(
        IOpenAiMappingService openAiMappingService,
        IDynamicsPayloadValidator validator,
        IPayloadLoggingService payloadLoggingService,
        ILogger<SapAccountMappingHttpFunction> logger)
    {
        _openAiMappingService = openAiMappingService;
        _validator = validator;
        _payloadLoggingService = payloadLoggingService;
        _logger = logger;
    }

    [Function(nameof(SapAccountMappingHttpFunction))]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sap-account-map")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var message = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);

        SapServiceBusMessage sapMessage;
        try
        {
            sapMessage = DeserializeMessage(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize HTTP request payload. mappingStatus=DeserializationFailed");
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Invalid request payload. Ensure organizationAccount is present.", cancellationToken);
            return badRequest;
        }

        var correlationId = sapMessage.CorrelationId ?? "unknown";
        var eventType = sapMessage.EventType ?? "unknown";

        _logger.LogInformation("HTTP mapping started. correlationId={correlationId}, eventType={eventType}, sourceSystem={sourceSystem}, mappingStatus=Started",
            correlationId, eventType, sapMessage.SourceSystem ?? "unknown");

        var aiJson = await _openAiMappingService.MapSapToDynamicsJsonAsync(sapMessage, cancellationToken);
        var validation = _validator.ValidateAndNormalize(aiJson, out var dynamicsPayload, out _);

        if (!validation.IsValid || dynamicsPayload is null)
        {
            _logger.LogError("HTTP mapping validation failed. correlationId={correlationId}, eventType={eventType}, mappingStatus=ValidationFailed, validationErrors={validationErrors}",
                correlationId, eventType, string.Join(" | ", validation.Errors));

            var unprocessable = request.CreateResponse(HttpStatusCode.UnprocessableEntity);
            await unprocessable.WriteStringAsync($"Dynamics payload validation failed: {string.Join("; ", validation.Errors)}", cancellationToken);
            return unprocessable;
        }

        _payloadLoggingService.LogFinalPayload(correlationId, eventType, dynamicsPayload);
        _logger.LogInformation("HTTP mapping completed. correlationId={correlationId}, eventType={eventType}, mappingStatus=Succeeded", correlationId, eventType);

        var ok = request.CreateResponse(HttpStatusCode.OK);
        await ok.WriteAsJsonAsync(dynamicsPayload, cancellationToken);
        return ok;
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
