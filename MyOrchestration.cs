using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DurableFunctionMultiEventRepro
{
    public static class MyOrchestration
    {
        [Function(nameof(MyOrchestration))]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var logger = context.CreateReplaySafeLogger(nameof(MyOrchestration));
            logger.LogInformation("Orchestrator started");

            logger.LogInformation("Listen for RecurringEvent for the first time");
            var cts1 = new CancellationTokenSource();
            var unusedRecurringEvent1 = context.WaitForExternalEvent<object>("RecurringEvent", cts1.Token);

            logger.LogInformation("Await OneTimeEvent1");
            await context.WaitForExternalEvent<object>("OneTimeEvent1", CancellationToken.None);
            logger.LogInformation("Received OneTimeEvent1");
            
            logger.LogInformation("Cancel first RecurringEvent listener");
            cts1.Cancel();
            cts1.Dispose();

            logger.LogInformation("Listen for RecurringEvent for the second time");
            var cts2 = new CancellationTokenSource();
            var unusedRecurringEvent2 = context.WaitForExternalEvent<object>("RecurringEvent", cts2.Token);

            logger.LogInformation("Await OneTimeEvent2");
            await context.WaitForExternalEvent<object>("OneTimeEvent2", CancellationToken.None);
            logger.LogInformation("Received OneTimeEvent2");

            logger.LogInformation("Cancel second RecurringEvent listener");
            cts2.Cancel();
            cts2.Dispose();

            logger.LogInformation("Listen for RecurringEvent for the third time");
            var recurringEvent3 = context.WaitForExternalEvent<object>("RecurringEvent", CancellationToken.None);

            logger.LogInformation("Await RecurringEvent");
            await recurringEvent3;
            logger.LogInformation("Received RecurringEvent");
        }

        [Function("MyOrchestration_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
            HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("MyOrchestration_HttpStart");

            // Function input comes from the request content.
            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(MyOrchestration));

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}