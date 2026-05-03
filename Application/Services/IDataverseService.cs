using Domain.Models;

namespace Application.Services;

public interface IDataverseService
{
    Task UpsertAccountAsync(DynamicsAccountPayload payload, CancellationToken cancellationToken);
}
