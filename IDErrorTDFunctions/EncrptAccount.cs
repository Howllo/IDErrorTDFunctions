using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab.Plugins.CloudScript;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

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
            //Networking using 
            var context = await FunctionContext<dynamic>.Create(req);
            var args = context.FunctionArgument;
            string PlayerEmail = "", PlayerPassword = "", encryptedEmail = "", encryptedPassword = "";
            string stringReturn = "", EncryptionOrDecryption = "", privateKey = "", SingleOrDouble = "";

            //Convert Dynamic to String - Parse the string.
            var jsonString = JsonConvert.SerializeObject(args);
            dynamic data = JObject.Parse(jsonString);
            SingleOrDouble = data.SingleOrDouble;
            EncryptionOrDecryption = data.EncryptionOrDecryption;

            if (SingleOrDouble == "Double")
            {
                if (EncryptionOrDecryption == "Encryption")
                {
                    PlayerEmail = data.GetPlayerAccountEmail;
                    PlayerPassword = data.GetPlayerAccountPass;
                    privateKey = data.GetID;

                    //Encryption
                    encryptedEmail = EncryptString(PlayerEmail, privateKey, log);
                    encryptedPassword = EncryptString(PlayerPassword, privateKey, log);
                    stringReturn = "{\"GetPlayerAccountEmail\":\"" + encryptedEmail + "\",\"GetPlayerAccountPass\":\"" + encryptedPassword + "\"}";
                }
                else if (EncryptionOrDecryption == "Decryption")
                {
                    encryptedEmail = data.GetPlayerAccountEmail;
                    encryptedPassword = data.GetPlayerAccountPass;
                    privateKey = data.GetID;

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
                    PlayerEmail = data.GetIncomingInfo;
                    privateKey = data.GetID;
                    encryptedEmail = EncryptString(PlayerEmail, privateKey, log);
                    stringReturn = "{\"GetIncomingInfo\":\"" + encryptedEmail + "\"}";
                }
                else if (EncryptionOrDecryption == "Decryption")
                {
                    encryptedEmail = data.GetIncomingInfo;
                    privateKey = data.GetID;
                    PlayerEmail = DecryptString(encryptedEmail, privateKey);
                    stringReturn = "{\"GetIncomingInfo\":\"" + PlayerEmail + "\"}";
                }
            }
            return stringReturn;
        }

        public static string EncryptString(string plainText, string key, ILogger log)
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
