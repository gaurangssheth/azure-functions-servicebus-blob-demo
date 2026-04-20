using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace OrderIntegrationFunction
{
    public class OrderBlobService : IOrderBlobService
    {
        private readonly BlobSettings blobSettings;
        private readonly ILogger<OrderBlobService> logger;

        public OrderBlobService(IOptions<BlobSettings> blobOptions,
            ILogger<OrderBlobService> logger)
        {
            blobSettings = blobOptions.Value;
            this.logger = logger;
        }

        public async Task<string> SaveOrderPayloadAsync(string requestBody)
        {
            logger.LogInformation("Saving order payload to container {ContainerName}", blobSettings.IncomingContainerName);
            var blobConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnection");

            if (string.IsNullOrWhiteSpace(blobConnectionString))
            {
                throw new InvalidOperationException("BlobStorageConnection setting is missing.");
            }

            var blobName = $"order-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json";

            var blobServiceClient = new BlobServiceClient(blobConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(blobSettings.IncomingContainerName);

            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(blobName);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
            await blobClient.UploadAsync(stream, overwrite: true);

            return blobName;
        }

        public async Task<string?> ReadOrderPayloadAsync(string blobName)
        {
            // NEW
            logger.LogInformation("Reading blob {BlobName} from container {ContainerName}", blobName, blobSettings.IncomingContainerName);
            var blobConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnection");

            if (string.IsNullOrWhiteSpace(blobConnectionString))
            {
                throw new InvalidOperationException("BlobStorageConnection setting is missing.");
            }

            var blobServiceClient = new BlobServiceClient(blobConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(blobSettings.IncomingContainerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
            {
                return null;
            }

            var downloadResult = await blobClient.DownloadContentAsync();
            return downloadResult.Value.Content.ToString();
        }

        public async Task MoveToProcessedAsync(string blobName)
        {
            // NEW
            logger.LogInformation(
                "Moving blob {BlobName} from {SourceContainer} to {TargetContainer}",
                blobName,
                blobSettings.IncomingContainerName,
                blobSettings.ProcessedContainerName);

            var blobConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnection");

            if (string.IsNullOrWhiteSpace(blobConnectionString))
            {
                throw new InvalidOperationException("BlobStorageConnection setting is missing.");
            }

            var blobServiceClient = new BlobServiceClient(blobConnectionString);

            var sourceContainer = blobServiceClient.GetBlobContainerClient(blobSettings.IncomingContainerName);
            var targetContainer = blobServiceClient.GetBlobContainerClient(blobSettings.ProcessedContainerName);

            await targetContainer.CreateIfNotExistsAsync();

            var sourceBlob = sourceContainer.GetBlobClient(blobName);
            var targetBlob = targetContainer.GetBlobClient(blobName);

            await targetBlob.StartCopyFromUriAsync(sourceBlob.Uri);
            await sourceBlob.DeleteIfExistsAsync();

        }

        public async Task MoveToFailedAsync(string blobName)
        {
            var blobConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnection");

            if (string.IsNullOrWhiteSpace(blobConnectionString))
            {
                throw new InvalidOperationException("BlobStorageConnection setting is missing.");
            }

            logger.LogWarning(
                "Moving blob {BlobName} from {SourceContainer} to {TargetContainer}",
                blobName,
                blobSettings.IncomingContainerName,
                blobSettings.FailedContainerName);

            var blobServiceClient = new BlobServiceClient(blobConnectionString);

            var sourceContainer = blobServiceClient.GetBlobContainerClient(blobSettings.IncomingContainerName);
            var targetContainer = blobServiceClient.GetBlobContainerClient(blobSettings.FailedContainerName);

            await targetContainer.CreateIfNotExistsAsync();

            var sourceBlob = sourceContainer.GetBlobClient(blobName);
            var targetBlob = targetContainer.GetBlobClient(blobName);

            await targetBlob.StartCopyFromUriAsync(sourceBlob.Uri);
            await sourceBlob.DeleteIfExistsAsync();
        }
    }
}