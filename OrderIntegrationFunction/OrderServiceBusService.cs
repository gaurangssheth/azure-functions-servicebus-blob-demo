using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderIntegrationFunction.Functions;

namespace OrderIntegrationFunction
{
    public class OrderServiceBusService : IOrderServiceBusService
    {
        private readonly ServiceBusSettings serviceBusSettings;
        private readonly ILogger<OrderServiceBusService> logger;

        public OrderServiceBusService(IOptions<ServiceBusSettings> serviceBusOptions, 
            ILogger<OrderServiceBusService> logger)
        {
            serviceBusSettings = serviceBusOptions.Value;
            this.logger = logger;
        }

        public async Task SendOrderCreatedAsync(CreateOrderRequest request, string blobName)
        {
            var serviceBusConnection = Environment.GetEnvironmentVariable("ServiceBusConnection");

            if (string.IsNullOrWhiteSpace(serviceBusConnection))
            {
                throw new InvalidOperationException("ServiceBusConnection setting is missing.");
            }

            // NEW - safe preview for diagnostics
            var preview = serviceBusConnection.Length > 40
                ? serviceBusConnection.Substring(0, 40)
                : serviceBusConnection;

            var looksValid =
                    serviceBusConnection.Contains("Endpoint=sb://", StringComparison.OrdinalIgnoreCase) &&
                    serviceBusConnection.Contains("SharedAccessKeyName=", StringComparison.OrdinalIgnoreCase) &&
                    serviceBusConnection.Contains("SharedAccessKey=", StringComparison.OrdinalIgnoreCase);

            if (!looksValid)
            {
                throw new InvalidOperationException("ServiceBusConnection does not look like a valid Service Bus connection string.");
            }
            
            await using var serviceBusClient = new ServiceBusClient(serviceBusConnection);
            ServiceBusSender sender = serviceBusClient.CreateSender(serviceBusSettings.QueueName);

            var payload = new
            {
                retryCount = request.RetryCount,
                correlationId = request.CorrelationId,
                customerEmail = request.CustomerEmail,
                amount = request.Amount,
                orderStatus = request.OrderStatus,
                blobName,
                createdAtUtc = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(payload);

            var message = new ServiceBusMessage(json)
            {
                Subject = "OrderEchoCreated"
            };

            try
            {
                await sender.SendMessageAsync(message);
            }
            catch (ServiceBusException sbEx)
            {
                throw new Exception(
                    $"ServiceBusException occurred. Reason: {sbEx.Reason}, IsTransient: {sbEx.IsTransient}",
                    sbEx);
            }
            catch (Exception ex)
            {
                throw new Exception("Unexpected error while sending message to Service Bus.", ex);
            }
        }

        public async Task RescheduleMessageAsync(object payload, int delaySeconds)
        {
            var serviceBusConnection = Environment.GetEnvironmentVariable("ServiceBusConnection");

            await using var client = new ServiceBusClient(serviceBusConnection);
            var sender = client.CreateSender(serviceBusSettings.QueueName);

            // increment retry count
            var json = JsonSerializer.Serialize(payload);

            var message = new ServiceBusMessage(json)
            {
                ScheduledEnqueueTime = DateTime.UtcNow.AddSeconds(delaySeconds)
            };

            await sender.SendMessageAsync(message);
        }
    }
}