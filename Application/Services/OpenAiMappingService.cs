using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed class OpenAiMappingService : IOpenAiMappingService
{
    private const string SystemPrompt = "You are an enterprise AI mapping engine for SAP to Microsoft Dynamics 365 Dataverse integration. Convert incoming SAP JSON payloads into Dynamics 365 Account JSON payloads. Return ONLY valid JSON. Do not include explanations, markdown, comments, or notes. Do not create fields outside the target schema. Do not hallucinate or invent missing values. If a source value is missing, return null. Keep numbers as numbers.";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiMappingService> _logger;

    public OpenAiMappingService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAiMappingService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> MapSapToDynamicsJsonAsync(SapServiceBusMessage sourceMessage, CancellationToken cancellationToken)
    {
        var endpoint = _configuration["AzureOpenAI:Endpoint"] ?? _configuration["AzureOpenAI__Endpoint"];
        var apiKey = _configuration["AzureOpenAI:ApiKey"] ?? _configuration["AzureOpenAI__ApiKey"];
        var deployment = _configuration["AzureOpenAI:DeploymentName"] ?? _configuration["AzureOpenAI__DeploymentName"];
        var apiVersion = _configuration["AzureOpenAI:ApiVersion"] ?? _configuration["AzureOpenAI__ApiVersion"] ?? "2024-10-21";

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deployment))
            throw new InvalidOperationException("Azure OpenAI configuration is missing.");

        var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";

        var sourceJson = JsonSerializer.Serialize(sourceMessage);
        var targetSchemaJson = """
{"name":"string","accountnumber":"string","telephone1":"string","fax":"string","emailaddress1":"string","websiteurl":"string","industrycode":"string","customertypecode":"string","statuscode":"string","address1_line1":"string","address1_city":"string","address1_stateorprovince":"string","address1_postalcode":"string","address1_country":"string","vsi_sapbusinesspartnerid":"string","vsi_sapcustomerid":"string","vsi_taxid":"string","vsi_businesslicensenumber":"string","vsi_customersegment":"string","vsi_customertier":"string","vsi_saleschannel":"string","creditlimit":"number"}
""";

        var requestPayload = new
        {
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = $"SAP payload:\n{sourceJson}\n\nTarget Dynamics 365 account schema:\n{targetSchemaJson}" }
            },
            temperature = 0,
            top_p = 1,
            max_tokens = 1200
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("api-key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Azure OpenAI call failed: {StatusCode} {ResponseBody}", response.StatusCode, responseBody);
            throw new InvalidOperationException("Azure OpenAI mapping request failed.");
        }

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            _logger.LogError("Azure OpenAI response does not contain choices.");
            throw new InvalidOperationException("Azure OpenAI response is invalid.");
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var messageElement) ||
            !messageElement.TryGetProperty("content", out var contentElement))
        {
            _logger.LogError("Azure OpenAI response does not contain message content.");
            throw new InvalidOperationException("Azure OpenAI response is missing message content.");
        }

        string? content = contentElement.ValueKind switch
        {
            JsonValueKind.String => contentElement.GetString(),
            JsonValueKind.Array => ExtractTextFromContentArray(contentElement),
            _ => null
        };

        return content ?? throw new InvalidOperationException("Azure OpenAI returned empty content.");
    }

    private static string? ExtractTextFromContentArray(JsonElement contentArray)
    {
        foreach (var item in contentArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            if (!item.TryGetProperty("type", out var typeElement) ||
                !string.Equals(typeElement.GetString(), "text", StringComparison.OrdinalIgnoreCase))
                continue;

            if (item.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                return textElement.GetString();
        }

        return null;
    }
}
