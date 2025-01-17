using Azure.Core;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.ResourceManager.AppService;
using System.Net.Http.Json;
using Azure.Identity;
using Microsoft.Azure.ServiceBus.Primitives;
using Microsoft.Azure.Amqp.Framing;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.Json;

namespace FunctionApp.Logic
{
    public class Functions
    {

        // /subscriptions/6242e95d-15dc-4729-b59c-fdd867a434d2/resourceGroups/azurefunctions/providers/Microsoft.Web/sites/functionapp20250112081533B/functions?api-version=2022-03-01

        // https://learn.microsoft.com/en-us/answers/questions/833338/enable-disable-functions-in-azure-programmically-i
        // https://learn.microsoft.com/en-us/azure/azure-functions/disable-function?tabs=portal

        // STOP function
        // https://learn.microsoft.com/en-us/answers/questions/1279910/stop-azure-azure-function-app-programatically-usin

        public record FunctionDTO(string Id, string Name, string Status, string Trigger);

        public async IAsyncEnumerable<FunctionDTO> GetListOfAllAsync(ArmClient client, string functionAppId)
        {
            var resource = client.GetWebSiteResource(new ResourceIdentifier(functionAppId));
            var funcs = resource.GetSiteFunctions();
            await foreach(var function in funcs.GetAllAsync())
            {
                string trigger = "";
                try
                {
                    // data.Config: {"name":"ShortRunningPeriodicJob","entryPoint":"FunctionApp.ShortRunningPeriodicJob.Run","scriptFile":"FunctionApp.dll","language":"dotnet-isolated","functionDirectory":"","bindings":[{"name":"myTimer","type":"timerTrigger","direction":"In","schedule":"*/30 * * * * *"}]}
                    var doc = JsonDocument.Parse(function.Data.Config);
                    var bindings = doc.RootElement.GetProperty("bindings");
                    for(int i = 0; i < bindings.GetArrayLength(); i++)
                    {
                        var schedule = bindings[i].GetProperty("schedule").GetRawText().TrimStart('\"').TrimEnd('\"');
                        if (trigger.Length > 0) trigger += "; ";
                        trigger += schedule;
                    }
                } catch(Exception) { }

                string functionName = function.Data.Name;
                try
                {
                    functionName = GetFunctionName(functionName);
                }
                catch(Exception) { }

                yield return new FunctionDTO(function.Id, functionName, (function.Data.IsDisabled ?? false) ? "Disabled" : "Enabled", trigger);
            }
        }
        
        
        public async Task<string> DisableAsync(ArmClient client, string functionAppId, string functionId)
        {
            var response = await EnableDisableAsync(client, functionAppId, functionId, "{\"properties\":\"disabled\"}");

            if (response.IsSuccessStatusCode)
                return "Success (disable)";
            else
                return "Failed:" + response.StatusCode + " " + (await response.Content.ReadAsStringAsync());
        }


        public async Task<string> EnableAsync(ArmClient client, string functionAppId, string functionId)
        {
            var response = await EnableDisableAsync(client, functionAppId, functionId, "{\"properties\":\"enabled\"}");

            if (response.IsSuccessStatusCode)
                return "Success (enabled)";
            else
                return "Failed:" + response.StatusCode + " " + (await response.Content.ReadAsStringAsync());
        }



        private async Task<HttpResponseMessage> EnableDisableAsync(ArmClient client, string functionAppId, string functionId, string putPayload)
        {
            var resource = client.GetWebSiteResource(new ResourceIdentifier(functionAppId));
            var func = await resource.GetSiteFunctionAsync(GetFunctionName(functionId));
            var data = func.Value.Data;


            var credentials = new DefaultAzureCredential();
            var tokenResult = await credentials.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);

            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://management.azure.com/subscriptions/")
            };

            httpClient.DefaultRequestHeaders.Remove("Authorization");
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenResult.Token}");
            httpClient.DefaultRequestHeaders.Add("x-functions-key", "0kyadiFUtMnf1uwVJ-E6cFfiI5cvd9fr2R5wDz7Sbp9yAzFu-WiO2w==");

            var Uri = $"{data.Id}/properties/state?api-version=2018-11-01";

            var response = await httpClient.PutAsync(Uri, new StringContent(putPayload, Encoding.UTF8, "application/json"));

            return response;
        }


        // Manually run https://learn.microsoft.com/en-us/azure/azure-functions/functions-manually-run-non-http?tabs=azure-portal
        // https://functionapp20250112081533b.azurewebsites.net/admin/functions/ShortRunningPeriodicJob

        // https://www.blueboxes.co.uk/how-to-use-azure-management-apis-in-c-with-azureidentity
        public async Task<HttpResponseMessage> RunFunction(ArmClient client, string functionAppId, string functionId)
        {
            var resource = client.GetWebSiteResource(new ResourceIdentifier(functionAppId));
            var func = await resource.GetSiteFunctionAsync(GetFunctionName(functionId));
            var data = func.Value.Data;

            var credentials = new DefaultAzureCredential();
            var tokenResult = await credentials.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);

            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://" + (new Uri(data.Href)).Host + "")
            };

            httpClient.DefaultRequestHeaders.Remove("Authorization");
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenResult.Token}");
            httpClient.DefaultRequestHeaders.Add("x-functions-key", "0kyadiFUtMnf1uwVJ-E6cFfiI5cvd9fr2R5wDz7Sbp9yAzFu-WiO2w==");

            var Uri = $"/admin/functions/{GetFunctionName(functionId)}?api-version=2023-11-01";

            var response = await httpClient.PostAsync(Uri, new StringContent("{}", Encoding.UTF8, "application/json"));

            return response;
        }


        public async IAsyncEnumerable<string> ReadFilesystemLogs(ArmClient client, string functionAppId, string functionId)
        {
            var resource = client.GetWebSiteResource(new ResourceIdentifier(functionAppId));
            var func = await resource.GetSiteFunctionAsync(GetFunctionName(functionId));
            var data = func.Value.Data;

            var credentials = new DefaultAzureCredential();
            var tokenResult = await credentials.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);

            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://" + (new Uri(data.Href)).Host.ToLower().Replace(".azurewebsites.net", ".scm.azurewebsites.net") + "/")
            };

            httpClient.DefaultRequestHeaders.Remove("Authorization");
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenResult.Token}");
            httpClient.DefaultRequestHeaders.Add("x-functions-key", "0kyadiFUtMnf1uwVJ-E6cFfiI5cvd9fr2R5wDz7Sbp9yAzFu-WiO2w==");

            var Uri = $"/api/logstream/application/functions/function/{GetFunctionName(functionId)}?api-version=2023-11-01";

            var response = await httpClient.GetStreamAsync(Uri);

            var sr = new StreamReader(response);

            
            while (true)
            {
                string line = "";
                bool doExit = false;
                try
                {
                    line = await sr.ReadLineAsync();
                    if (line == null)
                    {
                        line = "Log stream terminated.";
                        doExit = true;
                    }
                } 
                catch (Exception ex)
                {
                    line = ex.ToString();
                    doExit = true;
                }
                yield return line ?? "";
                if (doExit) break;
            }
        }

        private string GetFunctionName(string functionId)
        {
            if (functionId.Contains('/'))
            {
                var last = functionId.LastIndexOf('/');
                return functionId.Substring(last + 1);
            }

            throw new Exception($"Function Name not found in string: '{functionId}'");
        }
    }
}
