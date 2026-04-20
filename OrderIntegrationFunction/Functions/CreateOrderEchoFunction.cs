using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OrderIntegrationFunction.Functions
{
    public class CreateOrderEchoFunction
    {
        private readonly IOrderBlobService orderBlobService;
        private readonly IOrderServiceBusService orderServiceBusService;
        private readonly ServiceBusSettings serviceBusSettings;
        private readonly BlobSettings blobSettings;

        public CreateOrderEchoFunction(
            IOrderBlobService orderBlobService,
            IOrderServiceBusService orderServiceBusService,
            Microsoft.Extensions.Options.IOptions<ServiceBusSettings> serviceBusOptions,
            Microsoft.Extensions.Options.IOptions<BlobSettings> blobOptions)
        {
            this.orderBlobService = orderBlobService;
            this.orderServiceBusService = orderServiceBusService;
            this.serviceBusSettings = serviceBusOptions.Value;
            this.blobSettings = blobOptions.Value;
        }

        [FunctionName("CreateOrderEcho")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders/echo")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("CreateOrderEcho function received a request.");
            var correlationId = Guid.NewGuid().ToString();
            log.LogInformation("CorrelationId: {CorrelationId}", correlationId);

            string requestBody;
            using (var reader = new StreamReader(req.Body))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Request body is required."
                });
            }

            CreateOrderRequest? request;

            try
            {
                request = JsonSerializer.Deserialize<CreateOrderRequest>(
                    requestBody,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch (JsonException ex)
            {
                log.LogError(ex, "Invalid JSON received. Body: {Body}", requestBody);

                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Invalid JSON payload.",
                    detail = ex.Message
                });
            }

            if (request == null)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Request payload could not be parsed."
                });
            }

            if (string.IsNullOrWhiteSpace(request.CustomerEmail))
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "CustomerEmail is required."
                });
            }

            if (request.Amount <= 0)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Amount must be greater than zero."
                });
            }

            var greetingPrefix = Environment.GetEnvironmentVariable("GreetingPrefix")
                                 ?? "Hello from local config";

            string blobName;

            try
            {
                blobName = await orderBlobService.SaveOrderPayloadAsync(requestBody);
                log.LogInformation("Order payload saved to blob {BlobName}", blobName);

                try
                {
                    request.CorrelationId = correlationId;
                    request.CreatedAtUtc = DateTime.Now;
                    request.RetryCount = 0;
                    await orderServiceBusService.SendOrderCreatedAsync(request, blobName);
                    log.LogInformation("Order message sent to Azure Service Bus.");
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Failed to send message to Azure Service Bus.");

                    return new ObjectResult(new
                    {
                        success = false,
                        message = "Failed to send message to Azure Service Bus.",
                        error = ex.Message,
                        inner = ex.InnerException?.Message
                    })
                    {
                        StatusCode = StatusCodes.Status500InternalServerError
                    };
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to upload blob.");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
                 
            var response = new CreateOrderEchoResponse
            {
                CorrelationId = request.CorrelationId,
                Success = true,
                Message = $"{greetingPrefix}: Azure Function received the order successfully.",
                OrderId = Guid.NewGuid(),
                CustomerEmail = request.CustomerEmail,
                Amount = request.Amount,
                OrderStatus = request.OrderStatus,
                BlobName = blobName,
                EnvironmentName = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "null",
                EffectiveQueueName = serviceBusSettings.QueueName,
                EffectiveContainerName = blobSettings.IncomingContainerName
            };

            return new OkObjectResult(response);
        }
    }

    public class CreateOrderRequest
    {
        public string CorrelationId { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int OrderStatus { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int RetryCount { get; set; }
    }

    public class CreateOrderEchoResponse
    {
        public string CorrelationId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid OrderId { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int OrderStatus { get; set; }
        public string BlobName { get; set; } = string.Empty;
        public string QueueName { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public string EffectiveQueueName { get; set; } = string.Empty;
        public string EffectiveContainerName { get; set; } = string.Empty;
    }
}