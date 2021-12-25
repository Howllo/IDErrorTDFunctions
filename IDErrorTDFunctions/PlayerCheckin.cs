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
        static string GetTitleInfo = Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID", EnvironmentVariableTarget.Process), JSONString = "";
        static string PlayerID = "";

        [FunctionName("PlayerCheckin")]
        public static async Task<dynamic> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            ILogger log)
        {
            SetJsonData setJsonData = new SetJsonData();
            var context = await FunctionContext<dynamic>.Create(req);
            var args = context.FunctionArgument;
            JSONString = JsonConvert.SerializeObject(args);
            dynamic data = JObject.Parse(JSONString);
            string CONTEXT_TRACKER = data.WhatTypeOfTracker;
            PlayerID = context.CurrentPlayerId;

            //Create a link to Playfabs
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
            var WeeklyInfo = resultsTitleData.Result.Data["ConsecutiveLoginTable"];

            //Check User Data for a key.
            if (results.Result.Data.ContainsKey(CONTEXT_TRACKER))
            {
                JSONString = JsonConvert.SerializeObject(results.Result.Data[CONTEXT_TRACKER].Value);
                data = JObject.Parse(JSONString);
            } else if(!results.Result.Data.ContainsKey(CONTEXT_TRACKER))
            {
                data.CurrenStreak = 0;
                data.LoginBefore = DateTime.UtcNow.AddDays(1);
            }

            if (data.DailyOrMonthly == "Daily")
            {
                if(CONTEXT_TRACKER.Contains("Consecutive"))
                {
                    int compareTimes = DateTime.Compare(DateTime.UtcNow, data.LoginBefore);
                    //Early | Advance
                    if (compareTimes < 0)
                    {
                        setJsonData.CurrentStreak = data.CurrentStreak++;
                        if (data.CurrentStreak > data.HighestStreak)
                        {
                            setJsonData.HighestStreak = data.CurrentStreak;
                            GrantItems(data.Reward, data.RewardAmount, serverAPI, log);
                            setJsonData.RewardGranted = true;
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
                } else if (CONTEXT_TRACKER.Contains("Weekly"))
                {
                    int compareTimes = DateTime.Compare(data.LoginAfter, DateTime.Today);
                    if (compareTimes > 0) //If greater than 24 hours.
                    {
                        setJsonData.CurrentStreak = data.CurrentStreak++;
                        setJsonData.HighestStreak = data.CurrentStreak;
                        setJsonData.LoginAfter = DateTime.UtcNow.AddDays(1);
                        GrantItems(data.Reward, data.RewardAmount, serverAPI, log);
                        setJsonData.RewardGranted = true;
                    }
                }
            }
            else if(data.DailyOrMonthly == "Monthly")
            {
                int compareTimes = DateTime.Compare(data.LoginAfter, DateTime.Today);
                if(compareTimes > 0) //If greater than 24 hours.
                {
                    setJsonData.CurrentStreak = data.CurrentStreak++;
                    setJsonData.LoginAfter = DateTime.UtcNow.AddDays(1);
                    GrantItems(data.Reward, data.RewardAmount, serverAPI, log);
                    setJsonData.RewardGranted = true;
                }
            }

            //Convert Object to string to be upload to players context.
            string SetCurrentInfo = JsonConvert.SerializeObject(setJsonData);
            var requestUpdate = new UpdateUserDataRequest();
            requestUpdate.PlayFabId = context.CurrentPlayerId;
            requestUpdate.Data[CONTEXT_TRACKER] = SetCurrentInfo;
            var resultsUpdate = await serverAPI.UpdateUserDataAsync(requestUpdate);

            return SetCurrentInfo;
        }

        public static async void GrantItems(string items, int count, PlayFabServerInstanceAPI serverAPI, ILogger log)
        {
            log.LogInformation("Granting items: " + items);

            List<string> parse = new List<string>();
            for (int i = 0; i < count; i++)
                parse[i] = items;
            GrantItemsToUserRequest request = new GrantItemsToUserRequest();
            request.PlayFabId = PlayerID;
            request.ItemIds = parse;
            request.Annotation = "Granting items for checkin streak.";
            var results = await serverAPI.GrantItemsToUserAsync(request);
            log.LogInformation("Successfully Granted: " + results.Result.ItemGrantResults.ToString() + " to " + PlayerID);

            return;
        }
    }

    //Out-going JSON
    public class SetJsonData
    { 
        public DateTime LoginAfter { get; set; }
        public DateTime LoginBefore { get; set; }
        public uint CurrentStreak { get; set; }
        public uint HighestStreak { get; set; }
        public string CurrentWeek { get; set; }
        public string currentLevel { get; set; }
        public bool RewardGranted { get; set; }
    }
}