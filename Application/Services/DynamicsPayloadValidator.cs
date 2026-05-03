using System.Text.Json;
using Domain.Models;

namespace Application.Services;

public sealed class DynamicsPayloadValidator : IDynamicsPayloadValidator
{
    private static readonly HashSet<string> AllowedFields =
    [
        "name","accountnumber","telephone1","fax","emailaddress1","websiteurl","industrycode",
        "customertypecode","statuscode","address1_line1","address1_city","address1_stateorprovince",
        "address1_postalcode","address1_country","vsi_sapbusinesspartnerid","vsi_sapcustomerid","vsi_taxid",
        "vsi_businesslicensenumber","vsi_customersegment","vsi_customertier","vsi_saleschannel","creditlimit"
    ];

    public ValidationResult ValidateAndNormalize(
        string aiJson,
        SapServiceBusMessage sourceMessage,
        out DynamicsAccountPayload? payload,
        out string normalizedJson)
    {
        payload = null;
        normalizedJson = string.Empty;
        var result = new ValidationResult();

        JsonDocument doc;
        try { doc = JsonDocument.Parse(aiJson); }
        catch (Exception ex)
        {
            result.Errors.Add($"Invalid JSON: {ex.Message}");
            return result;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                result.Errors.Add("AI output must be a JSON object.");
                return result;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
                if (!AllowedFields.Contains(prop.Name)) result.Errors.Add($"Field not allowed: {prop.Name}");

            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var field in AllowedFields)
            {
                if (!doc.RootElement.TryGetProperty(field, out var el)) { dict[field] = null; continue; }

                dict[field] = el.ValueKind switch
                {
                    JsonValueKind.String => string.IsNullOrWhiteSpace(el.GetString()) ? null : el.GetString(),
                    JsonValueKind.Null => null,
                    JsonValueKind.Number when field == "creditlimit" => el.GetDecimal(),
                    JsonValueKind.Number => AddTypeErrorAndReturnNull(field, "string or null", result),
                    JsonValueKind.True or JsonValueKind.False => AddTypeErrorAndReturnNull(field, "string or null", result),
                    _ => null
                };

                if (field == "creditlimit" && el.ValueKind is not (JsonValueKind.Number or JsonValueKind.Null))
                    result.Errors.Add("creditlimit must be numeric or null.");
            }

            foreach (var required in new[] { "name", "accountnumber", "vsi_sapbusinesspartnerid", "vsi_sapcustomerid" })
                if (dict[required] is null) result.Errors.Add($"Missing required field: {required}");

            ValidateAgainstSource(dict, sourceMessage, result);

            normalizedJson = JsonSerializer.Serialize(dict);
            payload = JsonSerializer.Deserialize<DynamicsAccountPayload>(normalizedJson);
        }

        return result;
    }

    private static object? AddTypeErrorAndReturnNull(string field, string expectedType, ValidationResult result)
    {
        result.Errors.Add($"Field {field} must be {expectedType}.");
        return null;
    }

    private static void ValidateAgainstSource(
        IReadOnlyDictionary<string, object?> dynamicsValues,
        SapServiceBusMessage sourceMessage,
        ValidationResult result)
    {
        var sourceFields = sourceMessage.OrganizationAccount?.Fields;
        if (sourceFields is null || sourceFields.Count == 0)
        {
            result.Errors.Add("Source organizationAccount is missing or empty.");
            return;
        }

        var sourceStrings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceNumbers = new HashSet<decimal>();

        foreach (var element in sourceFields.Values)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    var value = element.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        sourceStrings.Add(value.Trim());
                    break;
                case JsonValueKind.Number:
                    if (element.TryGetDecimal(out var number))
                        sourceNumbers.Add(number);
                    break;
            }
        }

        foreach (var (field, value) in dynamicsValues)
        {
            if (value is null)
                continue;

            if (field == "creditlimit")
            {
                if (value is decimal creditLimit && !sourceNumbers.Contains(creditLimit))
                    result.Errors.Add($"Field {field} is not grounded in source payload.");
                continue;
            }

            if (value is string textValue && !sourceStrings.Contains(textValue.Trim()))
                result.Errors.Add($"Field {field} is not grounded in source payload.");
        }
    }
}
