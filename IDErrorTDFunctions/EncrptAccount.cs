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
                    encryptedEmail = EncryptString(PlayerEmail, privateKey);
                    encryptedPassword = EncryptString(PlayerPassword, privateKey);
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
                    PlayerEmail = data.GetPlayerAccountEmail;
                    privateKey = data.GetID;
                    encryptedEmail = EncryptString(PlayerEmail, privateKey);
                    stringReturn = "{\"GetPlayerAccountEmail\":\"" + encryptedEmail + "\"}";
                }
                else if (EncryptionOrDecryption == "Decryption")
                {
                    encryptedEmail = data.GetPlayerAccountEmail;
                    privateKey = data.GetID;
                    PlayerEmail = DecryptString(encryptedEmail, privateKey);
                    stringReturn = "{\"GetPlayerAccountEmail\":\"" + PlayerEmail + "\"}";
                }
            }
            return stringReturn;
        }

        public static string EncryptString(string EncryptThis, string PrivateKey)
        {
            var saltStringBytes = Generate256bitsOfEntropy();
            var ivStringBytes = Generate256bitsOfEntropy();
            var plainTextBytes = Encoding.UTF8.GetBytes(EncryptThis);
            using (var password = new Rfc2898DeriveBytes(PrivateKey, saltStringBytes, DerivationmIterations))
            {
                var keyBytes = password.GetBytes(Keysize / 8);
                using (var symmetricKey = new RijndaelManaged())
                {
                    symmetricKey.BlockSize = 256;
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    using (var encryptor = symmetricKey.CreateEncryptor(keyBytes, ivStringBytes))
                    {
                        using (var memeoryStream = new MemoryStream())
                        {
                            using (var cryptoStream = new CryptoStream(memeoryStream, encryptor, CryptoStreamMode.Write))
                            {
                                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                                cryptoStream.FlushFinalBlock();
                                var cipherTextBytes = saltStringBytes;
                                cipherTextBytes = cipherTextBytes.Concat(ivStringBytes).ToArray();
                                cipherTextBytes = cipherTextBytes.Concat(memeoryStream.ToArray()).ToArray();
                                memeoryStream.Close();
                                cryptoStream.Close();
                                return Convert.ToBase64String(cipherTextBytes);
                            }
                        }
                    }
                }
            }
        }

        public static string DecryptString(string cipherText, string PrivateKey)
        {
            // Get the complete stream of bytes that represent:
            // [32 bytes of Salt] + [32 bytes of IV] + [n bytes of CipherText]
            var cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);
            var saltStringBytes = cipherTextBytesWithSaltAndIv.Take(Keysize / 8).ToArray();
            var ivStringBytes = cipherTextBytesWithSaltAndIv.Skip(Keysize / 8).Take(Keysize / 8).ToArray();
            var cipherTextBytes = cipherTextBytesWithSaltAndIv.Skip((Keysize / 8) * 2).Take(cipherTextBytesWithSaltAndIv.Length - ((Keysize / 8) * 2)).ToArray();

            using (var password = new Rfc2898DeriveBytes(PrivateKey, saltStringBytes, DerivationmIterations))
            {
                var keyBytes = password.GetBytes(Keysize / 8);
                using (var symmetricKey = new RijndaelManaged())
                {
                    symmetricKey.BlockSize = 256;
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    using (var decryptor = symmetricKey.CreateDecryptor(keyBytes, ivStringBytes))
                    {
                        using (var memoryStream = new MemoryStream(cipherTextBytes))
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                            {
                                var plainTextBytes = new byte[cipherTextBytes.Length];
                                var decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                                memoryStream.Close();
                                cryptoStream.Close();
                                return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
                            }
                        }
                    }
                }
            }
        }

        private static byte[] Generate256bitsOfEntropy()
        {
            var randomBytes = new byte[32];
            using (var rngCsp = new RNGCryptoServiceProvider())
            {
                rngCsp.GetBytes(randomBytes);
            }
            return randomBytes;
        }
    }
}
