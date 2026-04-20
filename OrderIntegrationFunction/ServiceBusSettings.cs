using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderIntegrationFunction
{
    public class ServiceBusSettings
    {
        public string QueueName { get; set; } = "orders-echo";
    }
}
