using System.Threading.Tasks;

namespace OrderIntegrationFunction
{
    public interface IOrderBlobService
    {
        Task<string> SaveOrderPayloadAsync(string requestBody);
        Task<string?> ReadOrderPayloadAsync(string blobName);
        Task MoveToProcessedAsync(string blobName);
        Task MoveToFailedAsync(string blobName);
        Task<bool> IdempotencyMarkerExistsAsync(string correlationId);
        Task CreateIdempotencyMarkerAsync(string correlationId);
    }
}