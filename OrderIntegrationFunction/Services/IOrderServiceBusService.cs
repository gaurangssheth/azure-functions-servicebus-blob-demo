using OrderIntegrationFunction.Functions;
using System.Threading.Tasks;

namespace OrderIntegrationFunction.Services
{
    public interface IOrderServiceBusService
    {
        Task SendOrderCreatedAsync(CreateOrderRequest request, string blobName);
        Task RescheduleMessageAsync(object payload, int delaySeconds);
        Task PublishOrderCreatedAsync(CreateOrderRequest request, string blobName, string correlationId);
    }
}