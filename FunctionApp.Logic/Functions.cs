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
                    // data.Config value example: {"name":"ShortRunningPeriodicJob","entryPoint":"FunctionApp.ShortRunningPeriodicJob.Run","scriptFile":"FunctionApp.dll","language":"dotnet-isolated","functionDirectory":"","bindings":[{"name":"myTimer","type":"timerTrigger","direction":"In","schedule":"*/30 * * * * *"}]}
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

                yield return new FunctionDTO(function.Id!, functionName, (function.Data.IsDisabled ?? false) ? "Disabled" : "Enabled", trigger);
            }
        }
        
        public record FunctionStatus(string Status);

        /// <summary>
        /// Disables Azure Function App Function
        /// </summary>
        /// <param name="client"></param>
        /// <param name="functionAppId"></param>
        /// <param name="functionId"></param>
        /// <returns></returns>
        public async Task<Result<FunctionStatus>> DisableAsync(ArmClient client, string functionAppId, string functionId)
        {
            // See:
            // https://learn.microsoft.com/en-us/answers/questions/833338/enable-disable-functions-in-azure-programmically-i
            // https://learn.microsoft.com/en-us/azure/azure-functions/disable-function?tabs=portal
            // https://learn.microsoft.com/en-us/answers/questions/1279910/stop-azure-azure-function-app-programatically-usin

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


        /// <summary>
        /// Enables Azure Function App Function
        /// </summary>
        /// <param name="client"></param>
        /// <param name="functionAppId"></param>
        /// <param name="functionId"></param>
        /// <returns></returns>
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

       
        /// <summary>
        /// Runs given Function App Function
        /// </summary>
        /// <param name="client"></param>
        /// <param name="functionAppId"></param>
        /// <param name="functionId"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> RunFunction(ArmClient client, string functionAppId, string functionId)
        {
            // See: https://learn.microsoft.com/en-us/azure/azure-functions/functions-manually-run-non-http?tabs=azure-portal
            var resource = client.GetWebSiteResource(new ResourceIdentifier(functionAppId));
            var func = await resource.GetSiteFunctionAsync(GetFunctionName(functionId));
            var data = func.Value.Data;

            using var httpClient = await this.CreateAuthorizedHttpClientWithMasterKey("https://" + (new Uri(data.Href)).Host);

            var Uri = $"/admin/functions/{GetFunctionName(functionId)}?api-version=2023-11-01";

            var response = await httpClient.PostAsync(Uri, new StringContent("{}", Encoding.UTF8, "application/json"));

            return response;
        }

        /// <summary>
        /// Reads "Filesystem Log" for a given Function App Function
        /// </summary>
        /// <param name="client"></param>
        /// <param name="functionAppId"></param>
        /// <param name="functionId"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<string> ReadFilesystemLogs(ArmClient client, string functionAppId, string functionId)
        {
            var resource = client.GetWebSiteResource(new ResourceIdentifier(functionAppId));
            var func = await resource.GetSiteFunctionAsync(GetFunctionName(functionId));
            var data = func.Value.Data;

            using var httpClient = await CreateAuthorizedHttpClientWithMasterKey("https://" + (new Uri(data.Href)).Host.ToLower().Replace(".azurewebsites.net", ".scm.azurewebsites.net") + "/");

            var Uri = $"/api/logstream/application/functions/function/{GetFunctionName(functionId)}?api-version=2023-11-01";

            var response = await httpClient.GetStreamAsync(Uri);

            var sr = new StreamReader(response);

            
            while (true)
            {
                string? line;
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

        private string ParsePropertiesValue(string responseContent)
        {
            var doc = JsonDocument.Parse(responseContent);
            var value = doc.RootElement.GetProperty("properties").GetRawText().TrimStart('"').TrimEnd('"');
            return value;
        }

        private async Task<HttpResponseMessage> SetPropertiesAync(ArmClient client, string functionAppId, string functionId, string propertiesValue)
        {
            using var httpClient = await CreateAuthorizedHttpClientWithMasterKey("https://management.azure.com/");

            var Uri = $"{functionId}/properties/state?api-version=2022-03-01";  // Example: "/subscriptions/6242e95d-15dc-4729-b59c-fdd867a434d2/resourceGroups/AzureFunctions/providers/Microsoft.Web/sites/functionapp20250112081533B/functions/AnotherJob/properties/state?api-version=2022-03-01"

            var payload = "{\"properties\":\"" + propertiesValue + "\"}";

            var response = await httpClient.PutAsync(Uri, new StringContent(payload, Encoding.UTF8, "application/json"));

            return response;
        }


        private async Task<HttpClient> CreateAuthorizedHttpClientWithMasterKey(string baseAddress)
        {
            HttpClient? client = null;
            try
            {
                var credentials = new DefaultAzureCredential();
                var tokenResult = await credentials.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);

                client = new HttpClient
                {
                    BaseAddress = new Uri(baseAddress)
                };

                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenResult.Token}");
                client.DefaultRequestHeaders.Add("x-functions-key", GetFunctionAppMasterKeySecret());

                return client;
            }
            catch (Exception)
            {
                client?.Dispose();
                throw;
            }
        }

        private string GetFunctionAppMasterKeySecret()
        {
            // See: https://learn.microsoft.com/en-us/azure/azure-functions/functions-manually-run-non-http?tabs=azure-portal#get-the-master-key
            return "0kyadiFUtMnf1uwVJ-E6cFfiI5cvd9fr2R5wDz7Sbp9yAzFu-WiO2w==";
        }

    }
}
