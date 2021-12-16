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
        [FunctionName("EncryptAccount")]
        public static async Task<dynamic> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            ILogger log)
        {
            var context = await FunctionContext<dynamic>.Create(req);
            var args = context.FunctionArgument;
            string PlayerEmail = "", PlayerPassword = "", encryptedEmail = "", encryptedPassword = "";
            string stringReturn = "", EncryptionOrDecryption = "", privateKey = "", SingleOrDouble = "", publicKey = "";
            bool GetPublic = false;
            string OfflineKeyForAllUsers = Environment.GetEnvironmentVariable("GET_PRIVATE_KEY_FOR_ALL_USER", EnvironmentVariableTarget.Process);

            //Convert Dynamic to String - Parse the string.
            var jsonString = JsonConvert.SerializeObject(args);
            dynamic data = JObject.Parse(jsonString);
            SingleOrDouble = data.SingleOrDouble;
            EncryptionOrDecryption = data.EncryptionOrDecryption;
            privateKey = data.GetID;
            GetPublic = data.GetPublic;

            if (SingleOrDouble == "Double")
            {
                if (EncryptionOrDecryption == "Encryption")
                {
                    PlayerEmail = data.GetPlayerAccountEmail;
                    PlayerPassword = data.GetPlayerAccountPass;

                    //Encryption
                    encryptedEmail = EncryptString(PlayerEmail, privateKey);
                    encryptedPassword = EncryptString(PlayerPassword, privateKey);
                    publicKey = EncryptString(privateKey, OfflineKeyForAllUsers);
                    stringReturn = "{\"GetPlayerAccountEmail\":\"" + encryptedEmail + "\",\"GetPlayerAccountPass\":\"" + encryptedPassword +
                                   "\",\"GetPublic\":\"" + publicKey + "\"}";
                }
                else if (EncryptionOrDecryption == "Decryption")
                {
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
                if (EncryptionOrDecryption == "Encryption" && !GetPublic)
                {
                    PlayerEmail = data.GetIncomingInfo;

                    //Encryptions
                    encryptedEmail = EncryptString(PlayerEmail, privateKey);
                    stringReturn = "{\"GetIncomingInfo\":\"" + encryptedEmail + "\"}";
                }
                else if (EncryptionOrDecryption == "Encryption" && GetPublic)
                {
                    PlayerEmail = data.GetIncomingInfo;

                    //Encryption
                    encryptedEmail = EncryptString(PlayerEmail, privateKey);
                    publicKey = EncryptString(privateKey, OfflineKeyForAllUsers);
                    stringReturn = "{\"GetIncomingInfo\":\"" + encryptedEmail + "\",\"GetPublic\":\"" + publicKey + "\"}";
                }
                else if (EncryptionOrDecryption == "Decryption")
                {
                    encryptedEmail = data.GetIncomingInfo;

                    //Decryption
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
