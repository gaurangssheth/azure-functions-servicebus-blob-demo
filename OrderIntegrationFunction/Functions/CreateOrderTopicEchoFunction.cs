using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrderIntegrationFunction.Services;

namespace OrderIntegrationFunction.Functions
{
    public class CreateOrderTopicEchoFunction
    {
        private readonly IOrderBlobService orderBlobService;
        private readonly IOrderServiceBusService orderServiceBusService;

        public CreateOrderTopicEchoFunction(
            IOrderBlobService orderBlobService,
            IOrderServiceBusService orderServiceBusService)
        {
            this.orderBlobService = orderBlobService;
            this.orderServiceBusService = orderServiceBusService;
        }

        [FunctionName("CreateOrderTopicEcho")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders/topic-echo")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("CreateOrderTopicEcho triggered.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var request = JsonSerializer.Deserialize<CreateOrderRequest>(
                requestBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult("Invalid request.");
            }

            var correlationId = Guid.NewGuid().ToString();

            // Save blob
            var blobName = await orderBlobService.SaveOrderPayloadAsync(requestBody);

            // Build payload for topic
            var payload = new
            {
                customerEmail = request.CustomerEmail,
                amount = request.Amount,
                orderStatus = request.OrderStatus,
                blobName = blobName,
                correlationId = correlationId,
                retryCount = 0,
                createdAtUtc = DateTime.UtcNow
            };

            // Publish to topic
            await orderServiceBusService.PublishOrderCreatedAsync(request, blobName, correlationId);

            log.LogInformation(
                "OrderCreated event published to topic. CorrelationId: {CorrelationId}, BlobName: {BlobName}",
                correlationId,
                blobName);

            return new OkObjectResult(new
            {
                success = true,
                message = "Order published to topic",
                correlationId,
                blobName
            });
        }
    }
}
