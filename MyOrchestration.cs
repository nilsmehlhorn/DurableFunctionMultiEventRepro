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

            logger.LogInformation("Listen for RecurringEvent");
            var cts = new CancellationTokenSource();
            var unusedRecurringEvent1 = context.WaitForExternalEvent<object>("RecurringEvent", cts.Token);

            logger.LogInformation("Awaiting OneTimeEvent");
            await context.WaitForExternalEvent<object>("OneTimeEvent", CancellationToken.None);
            logger.LogInformation("Received OneTimeEvent, cancelling first RecurringEvent listener");

            cts.Cancel();
            cts.Dispose();

            logger.LogInformation("Listen for RecurringEvent again");
            var recurringEvent2 = context.WaitForExternalEvent<object>("RecurringEvent", CancellationToken.None);

            logger.LogInformation("Awaiting RecurringEvent");
            await recurringEvent2;
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