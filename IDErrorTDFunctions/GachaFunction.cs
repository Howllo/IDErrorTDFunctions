using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab;
using PlayFab.ServerModels;
using PlayFab.Plugins.CloudScript;

namespace IDErrorTDFunctions
{
    public static class GachaFunction
    {
        static string DevSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process);
        static string GetTitleInfo = Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID", EnvironmentVariableTarget.Process);
        static int totalAmount = 1, Amount = 0;
        static List<string> items = new List<string>();

        [FunctionName("GachaFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            ILogger log)
        {
            var context = await FunctionContext<dynamic>.Create(req);
            var args = context.FunctionArgument;
            var stringInfo = JsonConvert.SerializeObject(args);
            dynamic data = JObject.Parse(stringInfo);
            string WhatBanner = data.WhatBanner;
            string PlayerID = data.PlayerID;
            Amount = data.Amount;

            //Server Info
            var apiSetting = new PlayFabApiSettings()
            {
                TitleId = GetTitleInfo,
                DeveloperSecretKey = DevSecretKey,
            };
            PlayFabAuthenticationContext titleContext = new PlayFabAuthenticationContext();
            var serverAPI = new PlayFabServerInstanceAPI(apiSetting, titleContext);

            if(WhatBanner != null)
            {
                for (int i = 0; i < Amount; i++)
                {
                    var request = new EvaluateRandomResultTableRequest()
                    {
                        CatalogVersion = "Characters",
                        TableId = WhatBanner
                    };
                    var results = await serverAPI.EvaluateRandomResultTableAsync(request);

                    //Grant Items
                    GrantItemToPlayer(results, PlayerID, log);
                    totalAmount++;
                }
            }

            //Create a Json to be returned.
            GetJsonInfo jsonCreator = new GetJsonInfo();
            foreach(var arrayInfo in items)
            {
                jsonCreator.currentItems.Add(arrayInfo);
            }
            string json = JsonConvert.SerializeObject(jsonCreator);

            return new OkObjectResult(json);
        }

        public static void GrantItemToPlayer(PlayFabResult<EvaluateRandomResultTableResult> tableResult, string playFabID, ILogger log)
        {
            if (totalAmount <= Amount)
            {
                items.Add(tableResult.Result.ResultItemId);
            }

            if (totalAmount == Amount)
            {
                GrantItemsToUserRequest request = new GrantItemsToUserRequest()
                {
                    PlayFabId = playFabID,
                    CatalogVersion = "Characters",
                    ItemIds = items
                };

                foreach (var item in items)
                {
                    log.LogInformation($"Granted item {item} to player {playFabID}.");
                }
            }
        }
    }

    public class GetJsonInfo
    {
        public List<string> currentItems; 
    }
}