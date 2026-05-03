using Domain.Models;

namespace Application.Services;

public interface IDynamicsPayloadValidator
{
    ValidationResult ValidateAndNormalize(
        string aiJson,
        SapServiceBusMessage sourceMessage,
        out DynamicsAccountPayload? payload,
        out string normalizedJson);
}
