using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using OrderIntegrationFunction.Services;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace OrderIntegrationFunction.Functions
{
    public class ProcessOrderEchoDeadLetterFunction
    {
        private readonly IOrderBlobService orderBlobService;

        public ProcessOrderEchoDeadLetterFunction(IOrderBlobService orderBlobService)
        {
            this.orderBlobService = orderBlobService;
        }

        [FunctionName("ProcessOrderEchoDeadLetter")]
        public async Task Run([ServiceBusTrigger("orders-echo/$DeadLetterQueue", Connection = "ServiceBusConnection")]string message,
            int deliveryCount,
            string messageId,
            ILogger log)
        {
            log.LogWarning("DLQ processor triggered. MessageId: {MessageId}, DeliveryCount: {DeliveryCount}",
                messageId,
                deliveryCount);
            log.LogWarning("Dead-letter processor triggered. Raw message: {Message}", message);

            var payload = JsonSerializer.Deserialize<OrderEchoQueueMessage>(
                message,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (payload == null)
            {
                log.LogWarning("DLQ payload could not be deserialized.");
                return;
            }

            log.LogWarning(
                "DLQ processing. CorrelationId: {CorrelationId}, DeliveryCount: {DeliveryCount}",
                payload.CorrelationId,
                deliveryCount);

            await orderBlobService.MoveToFailedAsync(payload.BlobName);

            log.LogWarning("Blob {BlobName} moved to failed container.", payload.BlobName);
        }
    }
}
