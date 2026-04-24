using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OrderIntegrationFunction.Services;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OrderIntegrationFunction.Functions
{
    public class ProcessBillingOrderCreatedFunction
    {
        private readonly IOrderBlobService orderBlobService;

        public ProcessBillingOrderCreatedFunction(IOrderBlobService orderBlobService)
        {
            this.orderBlobService = orderBlobService;
        }

        [FunctionName("ProcessBillingOrderCreated")]
        public async Task Run(
            [ServiceBusTrigger(
                "order-events",
                "billing-subscription",
                Connection = "ServiceBusTopicConnection")] string message,
            int deliveryCount,
            string messageId,
            ILogger log)
        {
            log.LogInformation(
                "Billing subscriber triggered. MessageId: {MessageId}, DeliveryCount: {DeliveryCount}",
                messageId,
                deliveryCount);

            var payload = System.Text.Json.JsonSerializer.Deserialize<OrderEchoQueueMessage>(
                message,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (payload == null)
            {
                throw new InvalidOperationException("Billing payload could not be deserialized.");
            }

            log.LogInformation(
                "Billing received OrderCreated. CorrelationId: {CorrelationId}, Email: {Email}, Amount: {Amount}, BlobName: {BlobName}",
                payload.CorrelationId,
                payload.CustomerEmail,
                payload.Amount,
                payload.BlobName);

            var blobContent = await orderBlobService.ReadOrderPayloadAsync(payload.BlobName);

            log.LogInformation(
                "Billing read blob content for CorrelationId {CorrelationId}: {BlobContent}",
                payload.CorrelationId,
                blobContent);
        }
    }
}
