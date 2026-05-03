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

    public ValidationResult ValidateAndNormalize(string aiJson, out DynamicsAccountPayload? payload, out string normalizedJson)
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
                    JsonValueKind.Number => el.GetRawText(),
                    JsonValueKind.True or JsonValueKind.False => el.GetBoolean().ToString().ToLowerInvariant(),
                    _ => null
                };

                if (field == "creditlimit" && el.ValueKind is not (JsonValueKind.Number or JsonValueKind.Null))
                    result.Errors.Add("creditlimit must be numeric or null.");
            }

            foreach (var required in new[] { "name", "accountnumber", "vsi_sapbusinesspartnerid", "vsi_sapcustomerid" })
                if (dict[required] is null) result.Errors.Add($"Missing required field: {required}");

            normalizedJson = JsonSerializer.Serialize(dict);
            payload = JsonSerializer.Deserialize<DynamicsAccountPayload>(normalizedJson);
        }

        return result;
    }
}
