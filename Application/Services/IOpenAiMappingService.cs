using Domain.Models;

namespace Application.Services;

public interface IOpenAiMappingService
{
    Task<string> MapSapToDynamicsJsonAsync(SapServiceBusMessage sourceMessage, CancellationToken cancellationToken);
}
