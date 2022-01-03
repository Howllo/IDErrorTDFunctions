using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.ServerModels;

namespace IDErrorTDFunctions
{
    public static class BundleToJson
    {
        static string DevSecret = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process);
        static string TitleIDInfo = Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID", EnvironmentVariableTarget.Process);
        static string[] Catagory = { "Characters", "Items" };
        static List<string> tempStringArray = new List<string>();
        static Dictionary<string, string> catagoryBundles = new Dictionary<string, string>();
        static int tempCountHolder = 0;

        [FunctionName("BundleToJson")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            ILogger log)
        {
            string itemNameID = "", lastItemName = "";

            log.LogInformation("Weekly Bundle organization.");
            var apiRequest = new PlayFabApiSettings()
            {
                DeveloperSecretKey = DevSecret,
                TitleId = TitleIDInfo,
            };
            var serverAPI = new PlayFabServerInstanceAPI(apiRequest);
            log.LogInformation("Passed server API");

            for(int i = 0; i < Catagory.Length; i++)
            {
                log.LogInformation("Entered loop.");
                var request = new GetCatalogItemsRequest()
                {
                    CatalogVersion = Catagory[i]
                };
                var results = await serverAPI.GetCatalogItemsAsync(request);

                log.LogInformation(results.Result.Catalog[i].ToString());

                //Go through each bundle and place them as 
                for (int k = 0; k < results.Result.Catalog.Count; k++)
                {
                    log.LogInformation("Entered First For.");
                    results.Result.Catalog[k].Bundle.BundledItems.Sort();
                    var item = results.Result.Catalog[k];
                    int x = 0;
                    for (int j = 0; j < item.Bundle.BundledItems.Count; j++)
                    {
                        log.LogInformation("Entered Second For.");
                        if (item.Bundle.BundledItems[j] != null)
                        {
                            itemNameID = item.Bundle.BundledItems[j];
                            log.LogInformation($"Entered Second For {itemNameID}.");
                            tempCountHolder++;
                            log.LogInformation("Entered After ItemNameID.");
                            for (int n = 0; n < tempStringArray.Count; n++)
                            {
                                log.LogInformation("Entered Third For.");
                                if (item.Bundle.BundledItems[j].ToString() != tempStringArray[n])
                                    tempStringArray[n] = item.Bundle.BundledItems[j];
                            }
                            if (itemNameID != lastItemName)
                            {
                                tempStringArray[x] = tempStringArray[x].ToString() + ">" + tempCountHolder.ToString();
                                x++;
                                tempCountHolder = 0;
                            }
                            lastItemName = item.Bundle.BundledItems[j];
                        }
                    }
                    log.LogInformation("Entered last loop.");
                    string tempString = string.Join('|', tempStringArray);
                    catagoryBundles.Add(item.Bundle.ToString(), tempString);
                }
            }
            log.LogInformation("Passed the for loop.");

            var data = JsonConvert.SerializeObject(catagoryBundles);
            var requestTwo = new SetTitleDataRequest()
            {
                TitleId = TitleIDInfo,
                Key = "BundleProcessed",
                Value = data
            };
            var resultsSetTitle = await serverAPI.SetTitleDataAsync(requestTwo);

            log.LogInformation("Weekly Bundle Procress completed.");
            return new OkObjectResult("Yep");
        }
    }
}
