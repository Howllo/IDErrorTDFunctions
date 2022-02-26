using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PlayFab.Plugins.CloudScript;
using PlayFab;

namespace IDErrorTDFunctions
{
    public static class CharacterExperienceLevel
    {
        [FunctionName("CharacterExperienceLevel")]
        public static async Task<dynamic> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            ILogger log)
        {
            var context = await FunctionContext<dynamic>.Create(req);
            var args = context.FunctionArgument;

            //Get Title Infromation, and Get Virtual Currency.
            var apiSettings = new PlayFabApiSettings()
            {
                TitleId = Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID", EnvironmentVariableTarget.Process),
                DeveloperSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process)
            };
            PlayFabAuthenticationContext titleContext = new PlayFabAuthenticationContext();
            var serverAPI = new PlayFabServerInstanceAPI(apiSettings, titleContext);
            var request = new PlayFab.ServerModels.GetUserInventoryRequest();
            request.PlayFabId = args;
            var results = await serverAPI.GetUserInventoryAsync(request);

            //Important Variables.
            //   Max Level in Game     Current Level                     Get Player Total Experience
            int maxLevelAllowed = 50, currentLevel = 1, totalPlayerEXP = results.Result.VirtualCurrency["XP"];
            int[] experienceRequirePerLevel = new int[maxLevelAllowed];
            int multiples = 1, lastExp = 0, totalEXP = 0, percentageOfLast = 0;
            string responsesMessage = "";

            //Assign required experience to array elements.
            for (int i = 0; i < maxLevelAllowed; i++)
            {
                percentageOfLast = (lastExp * multiples) / 100;
                percentageOfLast += 250;
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
