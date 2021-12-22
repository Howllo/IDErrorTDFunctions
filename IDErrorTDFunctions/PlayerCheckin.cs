using System;
using System.Net.Http;
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

            var apiSettings = new PlayFabApiSettings()
            {
                TitleId = GetTitleInfo,
                DeveloperSecretKey = DevSecretKey
            };
            var serverAPI = new PlayFabServerInstanceAPI(apiSettings);
            var request = new GetUserDataRequest();
            request.PlayFabId = context.CurrentPlayerId;
            var results = await serverAPI.GetUserReadOnlyDataAsync(request);

            if (!results.Result.Data.ContainsKey(CONTEXT_TRACKER))
            {
                JSONString = JsonConvert.SerializeObject(results.Result.Data[CONTEXT_TRACKER].Value);
                data = JObject.Parse(JSONString);
            }

            if (data.DailyOrMonthly == "Daily")
            {
                int compareTimes = DateTime.Compare(data.GetUTCTime, data.LoginBefore);
                if (compareTimes < 0) //Early 
                {
                    //Earlier than loginbefore
                    setJsonData.CurrentStreak = data.CurrentStreak++;
                    GrantItems(data.Reward, data.RewardAmount, log);
                    setJsonData.GetUTCTime = DateTime.UtcNow;
                    setJsonData.LoginBefore = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day + 2,
                                                           DateTime.Today.Hour, DateTime.Today.Minute, DateTime.Today.Second);
                }
                else if (compareTimes > 0) //Late | Reset
                {
                    setJsonData.CurrentStreak = 1;
                    setJsonData.GetUTCTime = DateTime.UtcNow;
                    setJsonData.LoginBefore = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day + 2,
                                                           DateTime.Today.Hour, DateTime.Today.Minute, DateTime.Today.Second);
                }
            }
            if(data.DailyOrMonthly == "Monthly")
            {
                int compareTimes = DateTime.Compare(data.GetUTCTime, DateTime.Today);
                if(compareTimes > 0) //If greater than 24 hours.
                {
                    setJsonData.GetUTCTime = DateTime.UtcNow;
                    setJsonData.LoginBefore = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day + 1,
                                                           DateTime.Today.Hour, DateTime.Today.Minute, DateTime.Today.Second);
                }
            }
            string SetCurrentInfo = JsonConvert.SerializeObject(setJsonData);

            var requestUpdate = new UpdateUserDataRequest();
            requestUpdate.PlayFabId = context.CurrentPlayerId;
            requestUpdate.Data[CONTEXT_TRACKER] = SetCurrentInfo;
            var resultsUpdate = await serverAPI.UpdateUserDataAsync(requestUpdate);
            
            return 0;
        }

        public static void GrantItems(string items, int count, ILogger log)
        {
            log.LogInformation("Granting items: " + items);
            
        }
    }

    //Out-going JSON
    public class SetJsonData
    { 
        public DateTime GetUTCTime { get; set; }
        public DateTime LoginBefore { get; set; }
        public string CheckTracker { get; set; }
        public int CurrentStreak { get; set; }
        public string DailyOrMonthly { get; set; }
        public string CurrentWeek { get; set; }
        public string currentLevel { get; set; }
    }
}
