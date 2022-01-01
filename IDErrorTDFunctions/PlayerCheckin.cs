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
        static string GetTitleInfo = Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID", EnvironmentVariableTarget.Process), PlayerContextString = "";
        static string PlayerID = "", Reward = "";
        static uint CurrentStreak = 0, HighestStreak = 0;
        static DateTime LoginBefore, LoginAfter;
        static bool isFirstTime = false;
        static dynamic titleData, TitleContextJson, PlayerContextJson;

        [FunctionName("PlayerCheckin")]
        public static async Task<dynamic> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            ILogger log)
        {
            //Incoming Data
            var context = await FunctionContext<dynamic>.Create(req);
            var args = context.FunctionArgument;

            //Get Context
            PlayerContextString = JsonConvert.SerializeObject(args);
            dynamic playerData = JObject.Parse(PlayerContextString);
            string PLAYER_CONTEXT_TRACKER = playerData.PlayerTrackerName, TITLE_CONTEXT_TRACKER = playerData.TitleDataTrackerName;
            string WeeklyOrMonthly = playerData.WeeklyOrMonthly;
            PlayerID = playerData.PlayerAccountID;

            //Create a link to Playfab
            var apiSettings = new PlayFabApiSettings()
            {
                TitleId = GetTitleInfo,
                DeveloperSecretKey = DevSecretKey
            };
            var serverAPI = new PlayFabServerInstanceAPI(apiSettings);

            //Get User Data
            var request = new GetUserDataRequest()
            {
                PlayFabId = PlayerID,
                Keys = null,
            };
            var results = await serverAPI.GetUserReadOnlyDataAsync(request);

            //Get Title Data
            var requestTitleData = new GetTitleDataRequest() 
            { 
                Keys = null
            };
            var resultsTitleData = await serverAPI.GetTitleDataAsync(requestTitleData);

            //Check if Title Data Exist
            if (resultsTitleData.Result.Data.ContainsKey(TITLE_CONTEXT_TRACKER))
            {
                TitleContextJson = JsonConvert.DeserializeObject(resultsTitleData.Result.Data[TITLE_CONTEXT_TRACKER]);
                titleData = JObject.Parse(TitleContextJson);
            }
            else
            {
                log.LogInformation($"There is current no type {TITLE_CONTEXT_TRACKER} active right now.");
                return null;
            }
        
            if (results.Result.Data.ContainsKey(PLAYER_CONTEXT_TRACKER))
            {
                log.LogInformation("Enter not first time");
                PlayerContextJson = JsonConvert.DeserializeObject(results.Result.Data[PLAYER_CONTEXT_TRACKER].ToString());
                playerData = JObject.Parse(PlayerContextString);
            }
            else if(!results.Result.Data.ContainsKey(PLAYER_CONTEXT_TRACKER))
            {
                isFirstTime = true;
            }

            //Set Data.
            if (!isFirstTime)
            {
                CurrentStreak = playerData.CurrentStreak; 
                HighestStreak =  playerData.HighestStreak;
                LoginBefore = Convert.ToDateTime(playerData.LoginBefore);
                LoginAfter = Convert.ToDateTime(playerData.LoginAfter);
                Reward = "";
            } else if (isFirstTime)
            {
                //Get Json Data | Other Variables
                CurrentStreak = 0;
                HighestStreak = 0;
                LoginBefore = DateTime.UtcNow.AddDays(1);
                Reward = "";
            }

            //Create Object.
            SetJsonData setJsonData = new SetJsonData();

            //Check and Send Rewards
            if (WeeklyOrMonthly == "Weekly")
            {
                if (TITLE_CONTEXT_TRACKER.Contains("Consecutive"))
                {
                    int compareTimes = DateTime.Compare(DateTime.UtcNow, LoginBefore);
                    //Early | Advance
                    if (compareTimes < 0)
                    {
                        if (!isFirstTime)
                            setJsonData.CurrentStreak = CurrentStreak++;
                        else if (isFirstTime)
                        {
                            Reward = RewardReturn(CurrentStreak, log);
                            GrantItems(Reward, serverAPI, log);
                            setJsonData.GrantedItem = Reward;
                            setJsonData.HighestStreak = CurrentStreak;
                        }

                        if (CurrentStreak > HighestStreak)
                        {
                            Reward = RewardReturn(CurrentStreak, log);
                            GrantItems(Reward, serverAPI, log);
                            setJsonData.GrantedItem = Reward;
                            setJsonData.HighestStreak = CurrentStreak;
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
                }
                else if (TITLE_CONTEXT_TRACKER.Contains("Event"))
                {
                    int compareTimes = DateTime.Compare(LoginAfter, DateTime.Today);
                    if (compareTimes > 0) //If greater than 24 hours.
                    {
                        if (!isFirstTime)
                            setJsonData.CurrentStreak = CurrentStreak++;
                        Reward = RewardReturn(CurrentStreak, log);
                        GrantItems(Reward, serverAPI, log);
                        setJsonData.GrantedItem = Reward;
                        setJsonData.HighestStreak = CurrentStreak;
                        setJsonData.LoginAfter = DateTime.UtcNow.AddDays(1);
                    }
                }
            }
            else if (WeeklyOrMonthly == "Monthly")
            {
                int compareTimes = DateTime.Compare(LoginAfter, DateTime.Today);
                if (compareTimes > 0) //If greater than 24 hours.
                {
                    if (!isFirstTime)
                        setJsonData.CurrentStreak = CurrentStreak++;
                    setJsonData.LoginAfter = DateTime.UtcNow.AddDays(1);
                    Reward = RewardReturn(CurrentStreak, log);
                    GrantItems(Reward, serverAPI, log);
                    setJsonData.GrantedItem = Reward;
                }
            }
            else
                setJsonData.GrantedItem = "";

            //Convert Object to JSon string to be upload to players context.
            string PlayerData = JsonConvert.SerializeObject(setJsonData);

            var requestUpdate = new UpdateUserDataRequest()
            {
                PlayFabId = PlayerID,
                Data = new Dictionary<string, string>()
                {
                    { PLAYER_CONTEXT_TRACKER, PlayerData },
                },
                Permission = UserDataPermission.Public

            };
            var resultsUpdate = await serverAPI.UpdateUserDataAsync(requestUpdate);
            return PlayerData;
        }

        public static async void GrantItems(string items, PlayFabServerInstanceAPI serverAPI, ILogger log)
        {
            bool hasGranted = false;
            log.LogInformation("Granting items: " + items + " to player " + PlayerID);

            if (!hasGranted)
            {
                GrantItemsToUserRequest request = new GrantItemsToUserRequest()
                {
                    PlayFabId = PlayerID,
                    CatalogVersion = CatalogReturn(CurrentStreak, log),
                    ItemIds = new List<string>()
                    {
                        items
                    },
                    Annotation = "Granting items for checkin streak.",
                };
                hasGranted = true;
                var results = await serverAPI.GrantItemsToUserAsync(request);
            }
            return;
        }

        //Get reward based on CurrentStreak.
        private static string RewardReturn(uint CurrentStreak, ILogger log)
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
                    reward = titleData["Day8"]["Reward"];
                    break;
                case 8:
                    reward = titleData["Day9"]["Reward"];
                    break;
                case 9:
                    reward = titleData["Day10"]["Reward"];
                    break;
                case 10:
                    reward = titleData["Day11"]["Reward"];
                    break;
                case 11:
                    reward = titleData["Day12"]["Reward"];
                    break;
                case 12:
                    reward = titleData["Day13"]["Reward"];
                    break;
                case 13:
                    reward = titleData["Day14"]["Reward"];
                    break;
                case 14:
                    reward = titleData["Day15"]["Reward"];
                    break;
                case 15:
                    reward = titleData["Day16"]["Reward"];
                    break;
                case 16:
                    reward = titleData["Day17"]["Reward"];
                    break;
                case 17:
                    reward = titleData["Day18"]["Reward"];
                    break;
                case 18:
                    reward = titleData["Day19"]["Reward"];
                    break;
                case 19:
                    reward = titleData["Day20"]["Reward"];
                    break;
                case 20:
                    reward = titleData["Day21"]["Reward"];
                    break;
                case 21:
                    reward = titleData["Day22"]["Reward"];
                    break;
                case 22:
                    reward = titleData["Day23"]["Reward"];
                    break;
                case 23:
                    reward = titleData["Day24"]["Reward"];
                    break;
                case 24:
                    reward = titleData["Day25"]["Reward"];
                    break;
                case 25:
                    reward = titleData["Day26"]["Reward"];
                    break;
                case 26:
                    reward = titleData["Day27"]["Reward"];
                    break;
                case 27:
                    reward = titleData["Day28"]["Reward"];
                    break;
                case 28:
                    reward = titleData["Day29"]["Reward"];
                    break;
                case 29:
                    reward = titleData["Day30"]["Reward"];
                    break;
                case 30:
                    reward = titleData["Day31"]["Reward"];
                    break;
            }
            return reward;
        }

        //Get catalog based on CurrentStreak.
        private static string CatalogReturn(uint CurrentStreak, ILogger log)
        {
            string catalog = "";

            switch (CurrentStreak)
            {
                case 0:
                    catalog = titleData["Day1"]["Catalog"];
                    break;
                case 1:
                    catalog = titleData["Day2"]["Catalog"];
                    break;
                case 2:
                    catalog = titleData["Day3"]["Catalog"];
                    break;
                case 3:
                    catalog = titleData["Day4"]["Catalog"];
                    break;
                case 4:
                    catalog = titleData["Day5"]["Catalog"];
                    break;
                case 5:
                    catalog = titleData["Day6"]["Catalog"];
                    break;
                case 6:
                    catalog = titleData["Day7"]["Catalog"];
                    break;
                case 7:
                    catalog = titleData["Day8"]["Catalog"];
                    break;
                case 8:
                    catalog = titleData["Day9"]["Catalog"];
                    break;
                case 9:
                    catalog = titleData["Day10"]["Catalog"];
                    break;
                case 10:
                    catalog = titleData["Day11"]["Catalog"];
                    break;
                case 11:
                    catalog = titleData["Day12"]["Catalog"];
                    break;
                case 12:
                    catalog = titleData["Day13"]["Catalog"];
                    break;
                case 13:
                    catalog = titleData["Day14"]["Catalog"];
                    break;
                case 14:
                    catalog = titleData["Day15"]["Catalog"];
                    break;
                case 15:
                    catalog = titleData["Day16"]["Catalog"];
                    break;
                case 16:
                    catalog = titleData["Day17"]["Catalog"];
                    break;
                case 17:
                    catalog = titleData["Day18"]["Catalog"];
                    break;
                case 18:
                    catalog = titleData["Day19"]["Catalog"];
                    break;
                case 19:
                    catalog = titleData["Day20"]["Catalog"];
                    break;
                case 20:
                    catalog = titleData["Day21"]["Catalog"];
                    break;
                case 21:
                    catalog = titleData["Day22"]["Catalog"];
                    break;
                case 22:
                    catalog = titleData["Day23"]["Catalog"];
                    break;
                case 23:
                    catalog = titleData["Day24"]["Catalog"];
                    break;
                case 24:
                    catalog = titleData["Day25"]["Catalog"];
                    break;
                case 25:
                    catalog = titleData["Day26"]["Catalog"];
                    break;
                case 26:
                    catalog = titleData["Day27"]["Catalog"];
                    break;
                case 27:
                    catalog = titleData["Day28"]["Catalog"];
                    break;
                case 28:
                    catalog = titleData["Day29"]["Catalog"];
                    break;
                case 29:
                    catalog = titleData["Day30"]["Catalog"];
                    break;
                case 30:
                    catalog = titleData["Day31"]["Catalog"];
                    break;
            }
            return catalog;
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
        public string GrantedItem { get; set; }
    }
}