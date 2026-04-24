using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrderIntegrationFunction.Services;

namespace OrderIntegrationFunction.Functions
{
    public class ReadOrderBlobFunction
    {
        private readonly IOrderBlobService orderBlobService;

        public ReadOrderBlobFunction(IOrderBlobService orderBlobService)
        {
            this.orderBlobService = orderBlobService;
        }

        [FunctionName("ReadOrderBlob")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders/blob/{blobName}")] HttpRequest req,
            string blobName,
            ILogger log)
        {
            log.LogInformation("ReadOrderBlob function received request for blob {BlobName}", blobName);

            try
            {
                var content = await orderBlobService.ReadOrderPayloadAsync(blobName);

                if (content == null)
                {
                    return new NotFoundObjectResult(new
                    {
                        success = false,
                        message = $"Blob '{blobName}' was not found."
                    });
                }

                return new OkObjectResult(new
                {
                    success = true,
                    blobName = blobName,
                    content = content
                });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to read blob {BlobName}", blobName);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}