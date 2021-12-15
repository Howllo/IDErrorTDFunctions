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

namespace IDErrorTDFunctions
{
    public static class GetOfflineEncryption
    {
        [FunctionName("GetOfflineEncryption")]
        public static async Task<dynamic> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            //Check if online or offline.
            var name = req.QueryString;
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string PlayerEmail = "", PlayerPassword = "", encryptedEmail = "", encryptedPassword = "";
            string stringReturn = "", SingleOrDouble = "", publicKey = "", decryptedPublicKey = "";
            string OfflineKeyForAllUsers = Environment.GetEnvironmentVariable("GET_PRIVATE_KEY_FOR_ALL_USER", EnvironmentVariableTarget.Process);
            SingleOrDouble = data.SingleOrDouble;
            publicKey = data.GetPublic;
            decryptedPublicKey = DecryptString(publicKey, OfflineKeyForAllUsers);

            if (SingleOrDouble == "Double")
            {
                encryptedEmail = data.GetPlayerAccountEmail;
                encryptedPassword = data.GetPlayerAccountPass;

                //Decryption
                PlayerEmail = DecryptString(encryptedEmail, decryptedPublicKey);
                PlayerPassword = DecryptString(encryptedPassword, decryptedPublicKey);
                stringReturn = "{\"GetPlayerAccountEmail\":\"" + PlayerEmail + "\",\"GetPlayerAccountPass\":\"" + PlayerPassword + "\"}";
            }
            else if (SingleOrDouble == "Single")
            {
                encryptedEmail = data.GetIncomingInfo;

                PlayerEmail = DecryptString(encryptedEmail, decryptedPublicKey);
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