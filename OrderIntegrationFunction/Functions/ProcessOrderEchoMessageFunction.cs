using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace OrderIntegrationFunction.Functions
{
    public class ProcessOrderEchoMessageFunction
    {
        private readonly IOrderBlobService orderBlobService;
        private readonly IOrderServiceBusService orderServiceBusService;

        // NEW
        public ProcessOrderEchoMessageFunction(IOrderBlobService orderBlobService)
        {
            this.orderBlobService = orderBlobService;
        }

        [FunctionName("ProcessOrderEchoMessage")]
        public async Task Run([ServiceBusTrigger("orders-echo", Connection = "ServiceBusConnection")]string message, int deliveryCount,
            string messageId, ILogger log)
        {
            log.LogWarning(
                    "ProcessOrderEchoMessage triggered. MessageId: {MessageId}, DeliveryCount: {DeliveryCount}",
                    messageId,
                    deliveryCount);

            log.LogInformation("ProcessOrderEchoMessage triggered.");
            log.LogInformation("Raw Service Bus message: {Message}", message);

            try
            {
                var payload = JsonSerializer.Deserialize<OrderEchoQueueMessage>(
                    message,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (payload == null)
                {
                    log.LogWarning("Message payload could not be deserialized.");
                    return;
                }

                log.LogInformation(
                    "Processing message. CorrelationId: {CorrelationId}, DeliveryCount: {DeliveryCount}",
                    payload.CorrelationId,
                    deliveryCount);

                // NEW - idempotency check
                if (string.IsNullOrWhiteSpace(payload.CorrelationId))
                {
                    log.LogError("CorrelationId is missing.");
                    throw new InvalidOperationException("CorrelationId is required for idempotency.");
                }

                var alreadyProcessed = await orderBlobService.IdempotencyMarkerExistsAsync(payload.CorrelationId);

                if (alreadyProcessed)
                {
                    log.LogWarning(
                        "Duplicate message detected. CorrelationId: {CorrelationId}. Skipping processing.",
                        payload.CorrelationId);

                    return;
                }

                // NEW - read blob referenced by the queue message
                var blobContent = await orderBlobService.ReadOrderPayloadAsync(payload.BlobName);

                if (string.IsNullOrWhiteSpace(blobContent))
                {
                    log.LogWarning("Blob {BlobName} was not found or was empty.", payload.BlobName);
                    throw new InvalidOperationException($"Blob '{payload.BlobName}' was not found or was empty.");
                }

                log.LogInformation("Blob content loaded for {BlobName}: {BlobContent}",
                    payload.BlobName,
                    blobContent);

                if (payload.CustomerEmail.Equals("fail@test.com", StringComparison.OrdinalIgnoreCase))
                {
                    log.LogWarning(
                        "Simulated failure. RetryCount: {RetryCount}, DeliveryCount: {DeliveryCount}",
                        payload.RetryCount,
                        deliveryCount);

                    if (payload.RetryCount >= 3)
                    {
                        log.LogError("Max retry reached. Sending to DLQ.");
                        throw new InvalidOperationException("Max retry reached.");
                    }

                    // Calculate exponential delay (2^retry seconds)
                    var delaySeconds = (int)Math.Pow(2, payload.RetryCount);

                    await orderServiceBusService.RescheduleMessageAsync(payload, delaySeconds);

                    log.LogWarning("Message rescheduled with delay {DelaySeconds}s", delaySeconds);

                    return; // IMPORTANT → don't throw
                }

                // NEW - mark as processed once business work succeeds
                await orderBlobService.CreateIdempotencyMarkerAsync(payload.CorrelationId);

                log.LogInformation(
                    "Idempotency marker created for CorrelationId: {CorrelationId}",
                    payload.CorrelationId);

                await orderBlobService.MoveToProcessedAsync(payload.BlobName);
                log.LogInformation("Blob {BlobName} moved to processed container.", payload.BlobName);

                log.LogInformation(
                    "Processed message for email {Email}, amount {Amount}, blob {BlobName}, created {CreatedAtUtc}",
                    payload.CustomerEmail,
                    payload.Amount,
                    payload.BlobName,
                    payload.CreatedAtUtc);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to process Service Bus message.");
                throw;
            }
        }
    }

    public class OrderEchoQueueMessage
    {
        public string CorrelationId { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int OrderStatus { get; set; }
        public string BlobName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public int RetryCount { get; set; }
    }
}
