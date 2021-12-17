using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace IDErrorTDFunctions
{
    public static class CheckPlayerConnection
    {
        [FunctionName("CheckPlayerConnection")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var name = req.QueryString;
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string str = data?.Ping;
            if (str == "Ping")
                str = "{\"Ping\":\"Pong\"}";

            return new OkObjectResult(str);
        }
    }
}
