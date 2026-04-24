using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderIntegrationFunction.Models
{
    public class ServiceBusTopicSettings
    {
        public string TopicName { get; set; } = "order-events";
        public string BillingSubscriptionName { get; set; } = "billing-subscription";
        public string NotificationSubscriptionName { get; set; } = "notification-subscription";
    }
}
