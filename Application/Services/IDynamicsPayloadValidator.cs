using Domain.Models;

namespace Application.Services;

public interface IDynamicsPayloadValidator
{
    ValidationResult ValidateAndNormalize(string aiJson, out DynamicsAccountPayload? payload, out string normalizedJson);
}
