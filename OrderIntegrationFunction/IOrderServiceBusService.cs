using OrderIntegrationFunction.Functions;
using System.Threading.Tasks;

namespace OrderIntegrationFunction
{
    public interface IOrderServiceBusService
    {
        Task SendOrderCreatedAsync(CreateOrderRequest request, string blobName);
        Task RescheduleMessageAsync(object payload, int delaySeconds);
    }
}