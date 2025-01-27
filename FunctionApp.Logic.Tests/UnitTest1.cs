using LanguageExt;
using System.Numerics;

namespace FunctionApp.Logic.Tests
{
    public class UnitTest1
    {
        [Fact]
        public async Task Test1()
        {
            var sut = new Functions();

/*            var q = @"AppRequests 
| project Id, FunctionAppId = _ResourceId, FunctionName = OperationName, TimeGenerated, Properties
| sort by TimeGenerated desc";
*/

            var q = @"AppRequests 
| project Id, FunctionAppId = _ResourceId, FunctionName = OperationName, TimeGenerated, Properties
| sort by TimeGenerated desc";

            /*
             AppTraces 
| sort by TimeGenerated 
             */

            var result = await sut.QueryWorkspaceAsync<XXX>(q, TimeSpan.FromHours(24));

            Assert.True(result.Count() > 0);
        }


        [Fact]
        public async Task TestAppinsignts()
        {
            var sut = new Functions();

            /*            var q = @"AppRequests 
            | project Id, FunctionAppId = _ResourceId, FunctionName = OperationName, TimeGenerated, Properties
            | sort by TimeGenerated desc";
            */

            var q = @"traces | where timestamp > ago(1h) | take 1000";

            /*
             AppTraces 
| sort by TimeGenerated 
             */

            //                 = new Azure.Core.ResourceIdentifier("/subscriptions/6242e95d-15dc-4729-b59c-fdd867a434d2/resourceGroups/AzureFunctions/providers/Microsoft.Web/sites/functionapp20250112081533B/functions/ShortRunningPeriodicJob");
            // https://portal.azure.com/#@vaclavstrgmail.onmicrosoft.com/resource/subscriptions/6242e95d-15dc-4729-b59c-fdd867a434d2/resourceGroups/AzureFunctions/providers/microsoft.insights/components/functionapp20250112081533B/overview
            Azure.Core.ResourceIdentifier resourceIdentifier 
                = new Azure.Core.ResourceIdentifier("/subscriptions/6242e95d-15dc-4729-b59c-fdd867a434d2/resourceGroups/AzureFunctions/providers/microsoft.insights/components/functionapp20250112081533B");

            var result = await sut.QueryAsync(resourceIdentifier, q, TimeSpan.FromHours(1));

            // {["2025-01-22T17:12:30.0089394Z","Executing 'Functions.ShortRunningPeriodicJob' (Reason='Timer fired at 2025-01-22T17:12:30.0084527+00:00', Id=03fc8197-70fe-445d-b10d-0e7432a451ac)",1,"trace","{\"EventId\":\"1\",\"EventName\":\"FunctionStarted\",\"ProcessId\":\"8156\",\"InvocationId\":\"03fc8197-70fe-445d-b10d-0e7432a451ac\",\"HostInstanceId\":\"ab309693-03cc-414b-98ca-cf0f06794079\",\"prop__invocationId\":\"03fc8197-70fe-445d-b10d-0e7432a451ac\",\"prop__{OriginalFormat}\":\"Executing '{functionName}' (Reason='{reason}', Id={invocationId})\",\"LogLevel\":\"Information\",\"Category\":\"Function.ShortRunningPeriodicJob\",\"prop__functionName\":\"Functions.ShortRunningPeriodicJob\",\"prop__reason\":\"Timer fired at 2025-01-22T17:12:30.0084527+00:00\"}",null,"ShortRunningPeriodicJob","827c25896b8d4aee5e504c2d5a9e1780","c05a7bf641c689ec","","","","","","","PC","","","0.0.0.0","","","","","functionapp20250112081533b","82e1384bed69e722559d773e9a5a335ef5011db698db480b1132e9fd433044d7","d0f8986e-0876-4d1d-b869-4215df547472","/subscriptions/6242e95d-15dc-4729-b59c-fdd867a434d2/resourcegroups/azurefunctions/providers/microsoft.insights/components/functionapp20250112081533b","506a2615-8209-492c-802a-29f488c7ab52","azurefunctions: 4.1036.3.23284","2b818eb5-d8e4-11ef-933a-000d3abb838f",1,"/subscriptions/6242e95d-15dc-4729-b59c-fdd867a434d2/resourcegroups/azurefunctions/providers/microsoft.insights/components/functionapp20250112081533b"]}

            var xxx = result.Table.Rows.ToArray().Where(a => a.ToString().IndexOf("XXX") > 0).ToArray();

            var yyy = result.Table.Rows.Skip(result.Table.Rows.Count() - 20).ToArray();

            Assert.True(result.Table.Rows.Count() > 0);
        }

        private class XXX
        {
            public string Id { get; set; } = "";
            public string FunctionAppId { get; set; } = "";
            public string FunctionName { get; set; } = "";
            public string TimeGenerated { get; set; } = "";
            public string Properties { get; set; } = "";

        }

        // https://stackoverflow.com/questions/59504687/retrieve-azure-appinsights-live-metrics-through-api
        /*
         using Azure.Monitor.OpenTelemetry.LiveMetrics;

public class LiveMetricsExample
{
    public static async Task Main(string[] args)
    {
        var client = new LiveMetricsClient("InstrumentationKey=<Your Instrumentation Key>");

        await client.SubscribeAsync(
            new List<string> { "requests/count" },
            (metricData) =>
            {
                foreach (var metric in metricData)
                {
                    Console.WriteLine($"{metric.Name}: {metric.Value}");
                }
            }
        );

        Console.WriteLine("Press any key to unsubscribe...");
        Console.ReadKey();

        await client.UnsubscribeAsync();
    }
}
         */
    }
}