using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IDErrorTDFunctions
{
    public static class GetOfflineEncryption
    {
        [FunctionName("GetOfflineEncryption")]
        public static async Task<dynamic> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Enter the Encryption Function!");
            //Check if online or offline.
            var jsonString = JsonConvert.SerializeObject(req);
            log.LogInformation("Json String: " + jsonString);
            dynamic data = JObject.Parse(jsonString);
            log.LogInformation("Passed dynamic data. ");
            bool offline;
            offline = Convert.ToBoolean(data.Offline);
            log.LogInformation("Pass the Dynamic Json.");

            string PlayerEmail = "", PlayerPassword = "", encryptedEmail = "", encryptedPassword = "";
            string stringReturn = "", privateKey = "", SingleOrDouble = "";
            string publicKey = "", decryptedPublic = "";
            string OfflineKeyForAllUsers = Environment.GetEnvironmentVariable("GET_PRIVATE_KEY_FOR_ALL_USER", EnvironmentVariableTarget.Process);
            log.LogInformation("Passed the variables");

            SingleOrDouble = data.SingleOrDouble;
            publicKey = data.GetPublic;
            privateKey = data.GetID;
            log.LogInformation(offline.ToString());
            log.LogInformation("Data Processing Passed");

            if (SingleOrDouble == "Double")
            {
                log.LogInformation("Entered double decyption offline.");
                encryptedEmail = data.GetPlayerAccountEmail;
                encryptedPassword = data.GetPlayerAccountPass;

                //Decryption
                PlayerEmail = DecryptString(encryptedEmail, decryptedPublic);
                PlayerPassword = DecryptString(encryptedPassword, decryptedPublic);
                stringReturn = "{\"GetPlayerAccountEmail\":\"" + PlayerEmail + "\",\"GetPlayerAccountPass\":\"" + PlayerPassword + "\"}";
            }
            else if (SingleOrDouble == "Single")
            {
                log.LogInformation("Entered single decyption offline.");
                decryptedPublic = DecryptString(publicKey, OfflineKeyForAllUsers);
                encryptedEmail = data.GetIncomingInfo;

                PlayerEmail = DecryptString(encryptedEmail, decryptedPublic);
                stringReturn = "{\"GetIncomingInfo\":\"" + PlayerEmail + "\"}";
            }
            return stringReturn;
        }

        public static string EncryptString(string plainText, string key)
        {
            byte[] iv = new byte[16];
            byte[] array;

            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(plainText);
                        }
                        array = memoryStream.ToArray();
                    }
                }
            }
            return Convert.ToBase64String(array);
        }

        public static string DecryptString(string cipherText, string key)
        {
            byte[] iv = new byte[16];
            byte[] buffer = Convert.FromBase64String(cipherText);

            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.IV = iv;
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream(buffer))
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader((Stream)cryptoStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}