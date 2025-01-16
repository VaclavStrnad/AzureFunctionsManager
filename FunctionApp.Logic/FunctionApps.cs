using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;


namespace FunctionApp.Logic
{
    public class FunctionApps
    {
        public async Task<IEnumerable<GenericResourceData>> GetListOfAllAsync(ArmClient client)
        {
            SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

            var res = subscription.GetGenericResources();
            var ress = res.Select(a => a.Data).ToArray();
            var funcApps = ress.Where(a => a.Kind == "functionapp" && a.ResourceType.Type == "sites");

            return funcApps;
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
