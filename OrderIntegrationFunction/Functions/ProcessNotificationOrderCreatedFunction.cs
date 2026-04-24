using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace OrderIntegrationFunction.Functions
{
    public class ProcessNotificationOrderCreatedFunction
    {
        [FunctionName("ProcessNotificationOrderCreated")]
        public async Task Run(
            [ServiceBusTrigger(
                "order-events",
                "notification-subscription",
                Connection = "ServiceBusTopicConnection")] string message,
            int deliveryCount,
            string messageId,
            ILogger log)
        {
            await Task.CompletedTask;

            log.LogInformation(
                "Notification subscriber triggered. MessageId: {MessageId}, DeliveryCount: {DeliveryCount}",
                messageId,
                deliveryCount);

            var payload = JsonSerializer.Deserialize<OrderEchoQueueMessage>(
                message,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (payload == null)
            {
                throw new InvalidOperationException("Notification payload could not be deserialized.");
            }

            log.LogInformation(
                "Notification would send email for OrderCreated. CorrelationId: {CorrelationId}, Email: {Email}, Amount: {Amount}",
                payload.CorrelationId,
                payload.CustomerEmail,
                payload.Amount);
        }
    }
}
