using Azure.Core;
using Azure.ResourceManager;
using System.Text;
using Azure.ResourceManager.AppService;
using Azure.Identity;
using System.Text.Json;
using LanguageExt.Common;

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
        
        public record FunctionStatus(string Status);
        

        public async Task<Result<FunctionStatus>> DisableAsync(ArmClient client, string functionAppId, string functionId)
        {
            var response = await SetPropertiesAync(client, functionAppId, functionId, "disabled");

            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var newValue = ParsePropertiesValue(responseString);
                return new FunctionStatus(newValue);
            }
            else
                return new Result<FunctionStatus>(new Exception(response.StatusCode + " " + responseString));
        }


        public async Task<Result<FunctionStatus>> EnableAsync(ArmClient client, string functionAppId, string functionId)
        {
            var response = await SetPropertiesAync(client, functionAppId, functionId, "enabled");

            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var newValue = ParsePropertiesValue(responseString);
                return new FunctionStatus(newValue);
            }
            else
                return new Result<FunctionStatus>(new Exception(response.StatusCode + " " + responseString));
        }



        private string ParsePropertiesValue(string responseContent)
        {
            var doc = JsonDocument.Parse(responseContent);
            var value = doc.RootElement.GetProperty("properties").GetRawText().TrimStart('"').TrimEnd('"');
            return value;
        }

        private async Task<HttpResponseMessage> SetPropertiesAync(ArmClient client, string functionAppId, string functionId, string propertiesValue)
        {
            var resource = client.GetWebSiteResource(new ResourceIdentifier(functionAppId));
            var func = await resource.GetSiteFunctionAsync(GetFunctionName(functionId));
            var data = func.Value.Data;


            var credentials = new DefaultAzureCredential();
            var tokenResult = await credentials.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);

            // /subscriptions/6242e95d-15dc-4729-b59c-fdd867a434d2/resourceGroups/azurefunctions/providers/Microsoft.Web/sites/functionapp20250112081533B/functions/ShortRunningPeriodicJob/properties/state?api-version=2022-03-01

            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://management.azure.com/")
            };

            httpClient.DefaultRequestHeaders.Remove("Authorization");
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenResult.Token}");
            httpClient.DefaultRequestHeaders.Add("x-functions-key", "0kyadiFUtMnf1uwVJ-E6cFfiI5cvd9fr2R5wDz7Sbp9yAzFu-WiO2w==");

            var Uri = $"{data.Id}/properties/state?api-version=2022-03-01";

            var payload = "{\"properties\":\"" + propertiesValue + "\"}";

            var response = await httpClient.PutAsync(Uri, new StringContent(payload, Encoding.UTF8, "application/json"));

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
