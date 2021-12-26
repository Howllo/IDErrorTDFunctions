using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    public static class PlayerCheckin
    {
        static string DevSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process);
        static string GetTitleInfo = Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID", EnvironmentVariableTarget.Process), PlayerContextJson = "";
        static string PlayerID = "", TitleContextJson = "";
        static dynamic titleData;

        [FunctionName("PlayerCheckin")]
        public static async Task<dynamic> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            ILogger log)
        {
            //Incoming Data
            SetJsonData setJsonData = new SetJsonData();
            var context = await FunctionContext<dynamic>.Create(req);
            var args = context.FunctionArgument;
            PlayerID = context.CurrentPlayerId;

            //Get Context
            PlayerContextJson = JsonConvert.SerializeObject(args);
            dynamic playerData = JObject.Parse(PlayerContextJson);
            string PLAYER_CONTEXT_TRACKER = playerData.PlayerTrackerName, TITLE_CONTEXT_TRACKER = playerData.TitleDataTrackerName;
            string WeeklyOrMonthly = playerData.WeeklyOrMonthly;

            //Create a link to Playfab
            var apiSettings = new PlayFabApiSettings()
            {
                TitleId = GetTitleInfo,
                DeveloperSecretKey = DevSecretKey
            };
            var serverAPI = new PlayFabServerInstanceAPI(apiSettings);

            //Get User Data
            var request = new GetUserDataRequest();
            request.PlayFabId = context.CurrentPlayerId;
            var results = await serverAPI.GetUserReadOnlyDataAsync(request);

            //Get Title Data
            var requestTitleData = new GetTitleDataRequest();
            var resultsTitleData = await serverAPI.GetTitleDataAsync(requestTitleData);
            var WeeklyInfo = resultsTitleData.Result.Data[TITLE_CONTEXT_TRACKER];

            //Check if Title Data Exist
            if (resultsTitleData.Result.Data.ContainsKey(TITLE_CONTEXT_TRACKER))
            {
                TitleContextJson = JsonConvert.SerializeObject(resultsTitleData.Result.Data[TITLE_CONTEXT_TRACKER]);
                titleData = JObject.Parse(TitleContextJson);
            }
            else
            {
                log.LogInformation($"There is current no type {TITLE_CONTEXT_TRACKER} active right now.");
                return null;
            }

            //Check User Data for a key.
            if (results.Result.Data.ContainsKey(PLAYER_CONTEXT_TRACKER))
            {
                PlayerContextJson = JsonConvert.SerializeObject(results.Result.Data[PLAYER_CONTEXT_TRACKER].Value);
                playerData = JObject.Parse(PlayerContextJson);
            } else if(!results.Result.Data.ContainsKey(PLAYER_CONTEXT_TRACKER))
            {
                playerData.CurrenStreak = 0;
                playerData.LoginBefore = DateTime.UtcNow.AddDays(1);
            }

            //Get Json Data | Other Variables
            uint CurrentStreak = playerData.CurrentStreak, HighestStreak = playerData.HighestStreak, RewardAmount = playerData.RewardAmount;
            string Reward = "";

            //Check and Send Rewards
            if (WeeklyOrMonthly == "Daily")
            {
                if(TITLE_CONTEXT_TRACKER.Contains("Consecutive"))
                {
                    int compareTimes = DateTime.Compare(DateTime.UtcNow, playerData.LoginBefore);
                    //Early | Advance
                    if (compareTimes < 0)
                    {
                        setJsonData.CurrentStreak = CurrentStreak++;

                        if (CurrentStreak > HighestStreak)
                        {
                            Reward = RewardReturn(CurrentStreak);
                            GrantItems(Reward, serverAPI, log);
                            setJsonData.HighestStreak = playerData.CurrentStreak;
                        }

                        setJsonData.LoginAfter = DateTime.UtcNow.AddDays(1);
                        setJsonData.LoginBefore = DateTime.UtcNow.AddDays(2);
                    } //Late | Reset
                    else if (compareTimes > 0)
                    {
                        setJsonData.CurrentStreak = 0;
                        setJsonData.LoginAfter = DateTime.UtcNow.AddDays(1);
                        setJsonData.LoginBefore = DateTime.UtcNow.AddDays(2);
                    }
                } else if (TITLE_CONTEXT_TRACKER.Contains("Event"))
                {
                    int compareTimes = DateTime.Compare(playerData.LoginAfter, DateTime.Today);
                    if (compareTimes > 0) //If greater than 24 hours.
                    {
                        setJsonData.CurrentStreak = CurrentStreak++;
                        Reward = RewardReturn(CurrentStreak);
                        GrantItems(Reward, serverAPI, log);
                        setJsonData.HighestStreak = CurrentStreak;
                        setJsonData.LoginAfter = DateTime.UtcNow.AddDays(1);
                    }
                }
            }
            else if(WeeklyOrMonthly == "Monthly")
            {
                int compareTimes = DateTime.Compare(playerData.LoginAfter, DateTime.Today);
                if(compareTimes > 0) //If greater than 24 hours.
                {
                    setJsonData.CurrentStreak = playerData.CurrentStreak++;
                    setJsonData.LoginAfter = DateTime.UtcNow.AddDays(1);
                    Reward = RewardReturn(CurrentStreak);
                    GrantItems(Reward, serverAPI, log);
                }
            }

            //Convert Object to JSon string to be upload to players context.
            string SetCurrentInfo = JsonConvert.SerializeObject(setJsonData);
            var requestUpdate = new UpdateUserDataRequest();
            requestUpdate.PlayFabId = context.CurrentPlayerId;
            requestUpdate.Data[PLAYER_CONTEXT_TRACKER] = SetCurrentInfo;
            var resultsUpdate = await serverAPI.UpdateUserDataAsync(requestUpdate);

            return SetCurrentInfo;
        }

        public static async void GrantItems(string items, PlayFabServerInstanceAPI serverAPI, ILogger log)
        {
            log.LogInformation("Granting items: " + items);

            //2D Array Needed.
            List<string> parse = new List<string>();
            parse.Add(items);

            GrantItemsToUserRequest request = new GrantItemsToUserRequest();
            request.PlayFabId = PlayerID;
            request.ItemIds = parse;
            request.Annotation = "Granting items for checkin streak.";
            var results = await serverAPI.GrantItemsToUserAsync(request);
            log.LogInformation("Successfully Granted: " + results.Result.ItemGrantResults.ToString() + " to " + PlayerID);

            return;
        }

        //Get reward based on CurrentStreak.
        private static string RewardReturn(uint CurrentStreak)
        {
            string reward = "";

            switch (CurrentStreak)
            {
                case 0:
                    reward = titleData["Day1"]["Reward"];
                    break;
                case 1:
                    reward = titleData["Day2"]["Reward"];
                    break;
                case 2:
                    reward = titleData["Day3"]["Reward"];
                    break;
                case 3:
                    reward = titleData["Day4"]["Reward"];
                    break;
                case 4:
                    reward = titleData["Day5"]["Reward"];
                    break;
                case 5:
                    reward = titleData["Day6"]["Reward"];
                    break;
                case 6:
                    reward = titleData["Day7"]["Reward"];
                    break;
                case 7:
                    reward = titleData.Day8.Reward;
                    break;
                case 8:
                    reward = titleData.Day9.Reward;
                    break;
                case 9:
                    reward = titleData.Day10.Reward;
                    break;
                case 10:
                    reward = titleData.Day11.Reward;
                    break;
                case 11:
                    reward = titleData.Day12.Reward;
                    break;
                case 12:
                    reward = titleData.Day13.Reward;
                    break;
                case 13:
                    reward = titleData.Day14.Reward;
                    break;
                case 14:
                    reward = titleData.Day15.Reward;
                    break;
                case 15:
                    reward = titleData.Day16.Reward;
                    break;
                case 16:
                    reward = titleData.Day17.Reward;
                    break;
                case 17:
                    reward = titleData.Day18.Reward;
                    break;
                case 18:
                    reward = titleData.Day19.Reward;
                    break;
                case 19:
                    reward = titleData.Day20.Reward;
                    break;
                case 20:
                    reward = titleData.Day21.Reward;
                    break;
                case 21:
                    reward = titleData.Day22.Reward;
                    break;
                case 22:
                    reward = titleData.Day23.Reward;
                    break;
                case 23:
                    reward = titleData.Day24.Reward;
                    break;
                case 24:
                    reward = titleData.Day25.Reward;
                    break;
                case 25:
                    reward = titleData.Day26.Reward;
                    break;
                case 26:
                    reward = titleData.Day27.Reward;
                    break;
                case 27:
                    reward = titleData.Day28.Reward;
                    break;
                case 28:
                    reward = titleData.Day29.Reward;
                    break;
                case 29:
                    reward = titleData.Day30.Reward;
                    break;
                case 30:
                    reward = titleData.Day31.Reward;
                    break;
            }

            return reward;
        }
    }

    //Out-going JSON
    [Serializable]
    public class SetJsonData
    { 
        public DateTime LoginAfter { get; set; }
        public DateTime LoginBefore { get; set; }
        public uint CurrentStreak { get; set; }
        public uint HighestStreak { get; set; }
        public uint CurrentWeek { get; set; }
    }
}