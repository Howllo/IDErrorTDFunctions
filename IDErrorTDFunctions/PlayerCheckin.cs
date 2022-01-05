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
            log.LogInformation($"Granting {items} to player {PlayerID}.");

            GrantItemsToUserRequest request = new GrantItemsToUserRequest()
            {
                PlayFabId = PlayerID,
                CatalogVersion = CatalogReturn(CurrentStreak, log),
                ItemIds = new List<string>()
                {
                    items
                },
            };

            var results = await serverAPI.GrantItemsToUserAsync(request);
            log.LogInformation($"Granted {items} to player {PlayerID} successful.");
            return;
        }

        //Get reward based on CurrentStreak.
        private static string RewardReturn(uint CurrentStreak, ILogger log)
        {
            string reward = "";

            reward = titleData.Day[CurrentStreak].Reward;
            
            return reward;
        }

        //Get catalog based on CurrentStreak.
        private static string CatalogReturn(uint CurrentStreak, ILogger log)
        {
            string catalog = "";

            catalog = titleData.Day[CurrentStreak].Catalog;

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