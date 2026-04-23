using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderIntegrationFunction
{
    public class BlobSettings
    {
        public string IncomingContainerName { get; set; } = "orders-incoming";
        public string ProcessedContainerName { get; set; } = "orders-processed";
        public string IdempotencyContainerName { get; set; } = "orders-idempotency";
        public string FailedContainerName { get; set; } = "orders-failed";
    }
}
