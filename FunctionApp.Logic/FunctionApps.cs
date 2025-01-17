using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;


namespace FunctionApp.Logic
{
    public class FunctionApps
    {
        public async IAsyncEnumerable<WebSiteData> GetListOfAllAsync(ArmClient client)
        {
            var subscription = await client.GetDefaultSubscriptionAsync();
            
            await foreach(var site in subscription.GetWebSitesAsync())
            {
                if(site.Data.Kind == "functionapp" && site.Data.ResourceType.Type == "sites")
                {
                    yield return site.Data;
                }
            }
        }


        public async Task<Azure.Response> StartAsync(ArmClient client, string id)
        {
            var resource = client.GetWebSiteResource(new ResourceIdentifier(id));
            return await resource.StartAsync();
        }

        public async Task<Azure.Response> StopAsync(ArmClient client, string id)
        {
            var resource = client.GetWebSiteResource(new ResourceIdentifier(id));
            return await resource.StopAsync();
        }

        public async Task<string> GetStatusAsync(ArmClient client, string id)
        {
            var resource = client.GetWebSiteResource(new ResourceIdentifier(id));
            var result = await resource.GetAsync();
            return result.Value.Data.State;
        }
    }
}
