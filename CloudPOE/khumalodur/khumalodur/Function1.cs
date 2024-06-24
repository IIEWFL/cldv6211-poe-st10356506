using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

//order processing workflow(not in order): Update inventory, Process payment, Confirm order, send notifications(orderConfirmation,
// paymentProcessed, InventoryUpdated, orderPlaced)

namespace khumalodur
{
    /*https://docs.oracle.com/en/applications/jd-edwards/cross-product/9.2/eotos/understanding-run-orchestrations.html
      https://www.youtube.com/watch?v=UQ4iBl7QMno
      https://www.youtube.com/watch?v=J5YKm4tyAn0
    */
    public static class OrderProcessingFunctions
    {
        //function for processing order
        [Function(nameof(OrderProcessingOrchestrator))]
        public static async Task<string> OrderProcessingOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(OrderProcessingOrchestrator));
            logger.LogInformation("Order processing workflow is running...");

            var orderId = context.GetInput<string>();

            //call activities to update inventory, process payment, confirm order, and send notifications
            await context.CallActivityAsync(nameof(UpdateInventory), orderId);
            await context.CallActivityAsync(nameof(ProcessPayment), orderId);
            await context.CallActivityAsync(nameof(ConfirmOrder), orderId);
            await context.CallSubOrchestratorAsync(nameof(NotificationOrchestrator), orderId);

            logger.LogInformation("Order processing workflow completed.");
            return $"Order {orderId} processed successfully.";
        }

        //function to update the inventory
        [Function(nameof(UpdateInventory))]
        public static string UpdateInventory([ActivityTrigger] string orderId, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("Updating inventory...");
            logger.LogInformation("Updating inventory for order", orderId);
            
            return $"Inventory updated for {orderId}.";
        }

        //function to process payment
        [Function(nameof(ProcessPayment))]
        public static string ProcessPayment([ActivityTrigger] string orderId, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("Processing Payment...");
            logger.LogInformation("Processing payment for order", orderId);
            
            return $"Payment processed for order {orderId}.";
        }

        //function to confirm order
        [Function(nameof(ConfirmOrder))]
        public static string ConfirmOrder([ActivityTrigger] string orderId, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("Confirm Order...");
            logger.LogInformation("Confirm order", orderId);
           
            return $"Order {orderId} confirmed.";
        }

        //function to send notifications
        [Function(nameof(NotificationOrchestrator))]
        public static async Task NotificationOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(NotificationOrchestrator));
            logger.LogInformation("Starting notification orchestration...");

            var orderId = context.GetInput<string>();

            //call activities to send various notifications
            await context.CallActivityAsync(nameof(SendOrderPlacedNotification), orderId);
            await context.CallActivityAsync(nameof(SendInventoryUpdatedNotification), orderId);
            await context.CallActivityAsync(nameof(SendPaymentProcessedNotification), orderId);
            await context.CallActivityAsync(nameof(SendOrderConfirmedNotification), orderId);

            logger.LogInformation("Notification workflow completed.");
        }

        //function to sent order placed notification
        [Function(nameof(SendOrderPlacedNotification))]
        public static string SendOrderPlacedNotification([ActivityTrigger] string orderId, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("SendOrderPlacedNotification");
            logger.LogInformation("Sending order placed notification for order {orderId}.", orderId);
           
            return $"Order placed notification sent for order {orderId}.";
        }

        //function to send inventory updated notifications
        [Function(nameof(SendInventoryUpdatedNotification))]
        public static string SendInventoryUpdatedNotification([ActivityTrigger] string orderId, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("SendInventoryUpdatedNotification");
            logger.LogInformation("Sending inventory updated notification for order {orderId}.", orderId);
          
            return $"Inventory updated notification sent for order {orderId}.";
        }

        //function to send payment processed notifications
        [Function(nameof(SendPaymentProcessedNotification))]
        public static string SendPaymentProcessedNotification([ActivityTrigger] string orderId, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("SendPaymentProcessedNotification");
            logger.LogInformation("Sending payment processed notification for order {orderId}.", orderId);
            
            return $"Payment processed notification sent for order {orderId}.";
        }

        //function to send order confirmation order
        [Function(nameof(SendOrderConfirmedNotification))]
        public static string SendOrderConfirmedNotification([ActivityTrigger] string orderId, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("SendOrderConfirmedNotification");
            logger.LogInformation("Sending order confirmed notification for order {orderId}.", orderId);
            
            return $"Order confirmed notification sent for order {orderId}.";
        }

        //this function is triggered by the http event to start the orchestration
        [Function("OrderProcessing_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("OrderProcessing_HttpStart");

            //extract orderId 
            var requestBody = await req.ReadAsStringAsync();
            var orderId = requestBody; 

            //create an order orchestration instance
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(OrderProcessingOrchestrator), orderId);

            logger.LogInformation("Started order processing orchestration with ID = '{instanceId}'.", instanceId);

         
            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
