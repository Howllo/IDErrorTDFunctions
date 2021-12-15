using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab.Plugins.CloudScript;

namespace IDErrorTDFunctions
{
    public static class EncryptAccount
    {
        //Encryption
        private const int Keysize = 256;
        private const int DerivationmIterations = 1000;

        [FunctionName("EncryptAccount")]
        public static async Task<dynamic> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            ILogger log)
        {
            var context = await FunctionContext<dynamic>.Create(req);
            var args = context.FunctionArgument;
            string PlayerEmail = "", PlayerPassword = "", encryptedEmail = "", encryptedPassword = "";
            string stringReturn = "", EncryptionOrDecryption = "", privateKey = "", SingleOrDouble = "";
            string publicKey = "", decryptedPublic = "";
            string OfflineKeyForAllUsers = Environment.GetEnvironmentVariable("GET_PRIVATE_KEY_FOR_ALL_USER", EnvironmentVariableTarget.Process);

            //Convert Dynamic to String - Parse the string.
            var jsonString = JsonConvert.SerializeObject(args);
            dynamic data = JObject.Parse(jsonString);
            SingleOrDouble = data.SingleOrDouble;
            EncryptionOrDecryption = data.EncryptionOrDecryption;
            privateKey = data.GetID;

            if (SingleOrDouble == "Double")
            {
                if (EncryptionOrDecryption == "Encryption")
                {
                    log.LogInformation("Entered double Encyption");
                    PlayerEmail = data.GetPlayerAccountEmail;
                    PlayerPassword = data.GetPlayerAccountPass;
                    log.LogInformation($"Player Email {PlayerEmail}");
                    log.LogInformation($"Current private key is {privateKey}");
                    log.LogInformation($"Current Gamename is {OfflineKeyForAllUsers}");

                    //Encryption
                    encryptedEmail = EncryptString(PlayerEmail, privateKey);
                    encryptedPassword = EncryptString(PlayerPassword, privateKey);
                    log.LogInformation($"Normal Encrpytion are successful.");
                    publicKey = EncryptString(privateKey, OfflineKeyForAllUsers);
                    log.LogInformation("Public Key encryption works");
                    stringReturn = "{\"GetPlayerAccountEmail\":\"" + encryptedEmail + "\",\"GetPlayerAccountPass\":\"" + encryptedPassword +
                                   "\",\"GetPublic\":\"" + publicKey + "\"}";
                }
                else if (EncryptionOrDecryption == "Decryption")
                {
                    log.LogInformation("Entered double Decyption online.");
                    decryptedPublic = DecryptString(publicKey, OfflineKeyForAllUsers);
                    encryptedEmail = data.GetPlayerAccountEmail;
                    encryptedPassword = data.GetPlayerAccountPass;

                    //Decryption
                    PlayerEmail = DecryptString(encryptedEmail, privateKey);
                    PlayerPassword = DecryptString(encryptedPassword, privateKey);
                    stringReturn = "{\"GetPlayerAccountEmail\":\"" + PlayerEmail + "\",\"GetPlayerAccountPass\":\"" + PlayerPassword + "\"}";
                } 
            }
            else if (SingleOrDouble == "Single")
            {
                if (EncryptionOrDecryption == "Encryption")
                {
                    log.LogInformation("Entered single Encyption");
                    PlayerEmail = data.GetIncomingInfo;
                    encryptedEmail = EncryptString(PlayerEmail, privateKey);
                    stringReturn = "{\"GetIncomingInfo\":\"" + encryptedEmail + "\"}";
                }
                else if (EncryptionOrDecryption == "Decryption")
                {
                    log.LogInformation("Entered single decyption online.");
                    encryptedEmail = data.GetIncomingInfo;

                    PlayerEmail = DecryptString(encryptedEmail, privateKey);
                    stringReturn = "{\"GetIncomingInfo\":\"" + PlayerEmail + "\"}";
                }
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
