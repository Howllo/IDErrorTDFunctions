using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PlayFab.Plugins.CloudScript;

namespace IDErrorTDFunctions.Function
{
    public static class GetPlayerAcccountLevel
    {
        [FunctionName("GetPlayerAcccountLevel")]
        public static async Task<dynamic> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            ILogger log)
        {
            var context = await FunctionContext<dynamic>.Create(req);
            var args = context.FunctionArgument;

            //Get Title Infromation, and Get Virtual Currency.
            var apiSettings = new PlayFab.PlayFabApiSettings()
            {
                TitleId = Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID", EnvironmentVariableTarget.Process),
                DeveloperSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process)
            };
            PlayFab.PlayFabAuthenticationContext titleContext = new PlayFab.PlayFabAuthenticationContext();
            //titleContext.EntityToken = args.EntityToken;
            var serverAPI = new PlayFab.PlayFabServerInstanceAPI(apiSettings, titleContext);
            var request = new PlayFab.ServerModels.GetUserInventoryRequest();
            request.PlayFabId = args;
            var results = await serverAPI.GetUserInventoryAsync(request);

            log.LogInformation("Currency: " + results.Result.VirtualCurrency["XP"] + "\n");
            log.LogInformation("Title: " + apiSettings.TitleId + " Dev Secret: " + apiSettings.DeveloperSecretKey + "\n");
            log.LogInformation("TitleContext: " + titleContext);

            //Important Variables.
            //   Max Level in Game     Current Level                     Get Player Total Experience
            int maxLevelAllowed = 60, currentLevel = 1, totalPlayerEXP = results.Result.VirtualCurrency["XP"];
            int[] experienceRequirePerLevel = new int[maxLevelAllowed];
            int multiples = 5, lastExp = 0, totalEXP = 0, percentageOfLast = 0;
            string responsesMessage = "";

            //Assign required experience to array elements.
            for (int i = 0; i < maxLevelAllowed; i++)
            {
                percentageOfLast = (lastExp * multiples) / 100;
                percentageOfLast += 300;
                totalEXP = lastExp + percentageOfLast;
                experienceRequirePerLevel[i] = totalEXP.RoundUpFunction();
                lastExp = totalEXP;
            }

            //Check experience require for level against the total experience for the player.
            //Subtract experiences from totalPlayer.
            for (int i = 0; i < maxLevelAllowed; i++)
            {
                totalPlayerEXP -= experienceRequirePerLevel[i];
                if (totalPlayerEXP < experienceRequirePerLevel[i])
                {
                    currentLevel = i;
                    log.LogInformation($"Run() - level: {currentLevel} - Player EXP: {totalPlayerEXP}");
                    return currentLevel + 1;
                }
            }

            //if player level is higher than last requirement, then execute this.
            if (totalPlayerEXP > experienceRequirePerLevel[maxLevelAllowed - 1])
            {
                currentLevel = maxLevelAllowed;
                log.LogInformation($"Run() - level: {currentLevel} - Player EXP: {totalPlayerEXP}");
                return currentLevel;
            }


            responsesMessage = "Error! Something went wrong with calculating current level!";
            log.LogInformation($"Run() - level: {responsesMessage}");
            return 0;
        }

        //Round number by to nearest 10.
        private static int RoundUpFunction(this int NumberNeededRounding)
        {
            return ((int)Math.Round(NumberNeededRounding / 10.0)) * 10;
        }
    }
}
