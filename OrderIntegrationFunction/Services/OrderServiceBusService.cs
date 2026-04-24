using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderIntegrationFunction.Functions;
using OrderIntegrationFunction.Models;

namespace OrderIntegrationFunction.Services
{
    public class OrderServiceBusService : IOrderServiceBusService
    {
        private readonly ServiceBusSettings serviceBusSettings;
        private readonly ServiceBusTopicSettings serviceBusTopicSettings;
        private readonly ILogger<OrderServiceBusService> logger;

        public OrderServiceBusService(
            IOptions<ServiceBusSettings> serviceBusOptions,
            IOptions<ServiceBusTopicSettings> serviceBusTopicOptions,
            ILogger<OrderServiceBusService> logger)
        {
            serviceBusSettings = serviceBusOptions.Value;
            serviceBusTopicSettings = serviceBusTopicOptions.Value;
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

        // NEW
        // NEW
        public async Task PublishOrderCreatedAsync(
            CreateOrderRequest request,
            string blobName,
            string correlationId)
        {
            var serviceBusConnection = Environment.GetEnvironmentVariable("ServiceBusTopicConnection");

            if (string.IsNullOrWhiteSpace(serviceBusConnection))
            {
                throw new InvalidOperationException("ServiceBusTopicConnection setting is missing.");
            }

            await using var serviceBusClient = new ServiceBusClient(serviceBusConnection);
            if (string.IsNullOrWhiteSpace(serviceBusTopicSettings.TopicName))
            {
                throw new InvalidOperationException("ServiceBusTopicSettings__TopicName is missing.");
            }
            var sender = serviceBusClient.CreateSender(serviceBusTopicSettings.TopicName);

            var payload = new
            {
                customerEmail = request.CustomerEmail,
                amount = request.Amount,
                orderStatus = request.OrderStatus,
                blobName,
                correlationId,
                retryCount = 0,
                createdAtUtc = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(payload);

            var message = new ServiceBusMessage(json)
            {
                MessageId = correlationId,
                CorrelationId = correlationId,
                Subject = "OrderCreated"
            };

            // NEW - custom properties used by subscription filters
            message.ApplicationProperties["EventType"] = "BillingRequired";
            message.ApplicationProperties["OrderStatus"] = request.OrderStatus;
            message.ApplicationProperties["Amount"] = request.Amount;

            await sender.SendMessageAsync(message);
        }

        // NEW
        public async Task PublishOrderNotificationRequiredAsync(
            CreateOrderRequest request,
            string blobName,
            string correlationId)
        {
            var connection = Environment.GetEnvironmentVariable("ServiceBusTopicConnection");

            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new InvalidOperationException("ServiceBusTopicConnection is missing.");
            }

            await using var client = new ServiceBusClient(connection);

            if (string.IsNullOrWhiteSpace(serviceBusTopicSettings.TopicName))
            {
                throw new InvalidOperationException("ServiceBusTopicSettings__TopicName is missing.");
            }

            var sender = client.CreateSender(serviceBusTopicSettings.TopicName);

            var payload = new
            {
                customerEmail = request.CustomerEmail,
                amount = request.Amount,
                orderStatus = request.OrderStatus,
                blobName,
                correlationId,
                eventType = "NotificationRequired",
                createdAtUtc = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(payload);

            var message = new ServiceBusMessage(json)
            {
                MessageId = $"{correlationId}-notification",
                CorrelationId = correlationId,
                Subject = "NotificationRequired"
            };

            // NEW - this must match subscription filter
            message.ApplicationProperties["EventType"] = "NotificationRequired";
            message.ApplicationProperties["OrderStatus"] = request.OrderStatus;
            message.ApplicationProperties["Amount"] = request.Amount;

            await sender.SendMessageAsync(message);
        }
    }
}